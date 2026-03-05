using System.Collections.Concurrent;

namespace Utility.Hocr;

/// <summary>
/// Manages temporary file storage for PDF processing sessions. Provides session-based
/// temporary directory and file creation with automatic background cleanup of destroyed sessions.
/// Accessed as a thread-safe singleton via <see cref="Instance"/>.
/// </summary>
public class TempData : IDisposable
{
    private static readonly Lazy<TempData> LazyInstance =
        new(() => new TempData(), LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly ConcurrentDictionary<string, string> _caches = new();

    private System.Timers.Timer CleanUpTimer;
    private int _disposed = 0;


    private TempData()
    {
        CleanUpTimer = new System.Timers.Timer();
        CleanUpTimer.Interval = 5000;
        CleanUpTimer.Elapsed += CleanUpTimer_Elapsed;
        CleanUpTimer.Start();


        //Adding some cleanup here on restart.
        if (Directory.Exists(TemporaryFilePath))
        {
            foreach(string directory in Directory.GetDirectories(TemporaryFilePath))
                try
                {
                    Directory.Delete(directory, true);
                }
                catch (Exception)
                {
                    //
                }
            return;
        }

        try
        {
            Directory.CreateDirectory(TemporaryFilePath);
        }
        catch (Exception)
        {
            throw new Exception("Cannot create Cache Folder");
        }
    }



    private string TemporaryFilePath { get; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache");

    /// <summary>
    /// Gets the singleton instance of <see cref="TempData"/>. The instance is created
    /// lazily on first access and is safe for concurrent use.
    /// </summary>
    public static TempData Instance => LazyInstance.Value;


    /// <summary>
    /// Creates a named subdirectory within an existing session's temporary folder.
    /// </summary>
    /// <param name="sessionName">The session identifier returned by <see cref="CreateNewSession"/>.</param>
    /// <param name="directoryName">The name of the subdirectory to create.</param>
    /// <returns>The full path to the newly created directory.</returns>
    /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
    /// <exception cref="Exception">The session does not exist or the directory already exists.</exception>
    public string CreateDirectory(string sessionName, string directoryName)
    {
        if (Volatile.Read(ref _disposed) == 1)
            throw new ObjectDisposedException(nameof(TempData));

        if (!_caches.TryGetValue(sessionName, out string cachePath))
            throw new Exception("Invalid Session.");

        string fullPath = Path.Combine(cachePath, directoryName);

        if (Directory.Exists(fullPath))
            throw new Exception("Directory Exists.");

        Directory.CreateDirectory(fullPath);

        return fullPath;
    }

    /// <summary>
    /// Creates a new temporary session with its own isolated directory on disk.
    /// The session directory is created under the application's cache folder.
    /// </summary>
    /// <returns>The session identifier, used to create temp files and to destroy the session later.</returns>
    /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
    public string CreateNewSession()
    {
        if (Volatile.Read(ref _disposed) == 1)
            throw new ObjectDisposedException(nameof(TempData));

        string sessionName = Guid.NewGuid().ToString("N");
        string folderPath = Path.Combine(TemporaryFilePath, sessionName);

        try
        {
            Directory.CreateDirectory(folderPath);
        }
        catch (Exception)
        {
            throw new Exception("Cannot Create Session Folder.");
        }

        if (!_caches.TryAdd(sessionName, folderPath))
            throw new Exception("Session already exist!");
        return sessionName;
    }

    /// <summary>
    /// Generates a unique temporary file path within the specified session's directory.
    /// The file is not created on disk; only the path is returned.
    /// </summary>
    /// <param name="sessionName">The session identifier returned by <see cref="CreateNewSession"/>.</param>
    /// <param name="extensionWithDot">The file extension including the leading dot (e.g., ".pdf").</param>
    /// <param name="folders">Reserved for future use.</param>
    /// <returns>The full path for the new temporary file.</returns>
    /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
    /// <exception cref="Exception">The session does not exist.</exception>
    public string CreateTempFile(string sessionName, string extensionWithDot, string folders = null)
    {
        if (Volatile.Read(ref _disposed) == 1)
            throw new ObjectDisposedException(nameof(TempData));

        if (!_caches.TryGetValue(sessionName, out string cachePath))
            throw new Exception("Invalid Session");
        string newFile = Path.Combine(cachePath, Guid.NewGuid().ToString("N") + extensionWithDot);
        return newFile;
    }



    private readonly ConcurrentQueue<String> _toDestroy = new();
    private int _cleanUpTimerRunning = 0;


    private void CleanUpTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
    {
        if (Volatile.Read(ref _disposed) == 1)
            return;

        if (Interlocked.CompareExchange(ref _cleanUpTimerRunning, 1, 0) != 0)
            return;

        try
        {
            CleanUpFiles();
        }
        catch (Exception)
        {
            //
        }
        finally
        {
            Interlocked.Exchange(ref _cleanUpTimerRunning, 0);
        }
    }

    /// <summary>
    /// Processes queued directory deletions. Iterates through all items currently in the queue,
    /// deleting unlocked directories and re-enqueuing any that still have locked files.
    /// Uses a bounded loop based on the current queue count to avoid re-processing
    /// items that were re-enqueued during this pass.
    /// </summary>
    private void CleanUpFiles()
    {
        int itemsToProcess = _toDestroy.Count;
        for (int i = 0; i < itemsToProcess; i++)
        {
            if (!_toDestroy.TryDequeue(out string directoryToDelete))
                break;

            try
            {
                if (!Directory.Exists(directoryToDelete))
                    continue;

                foreach (string filename in Directory.GetFiles(directoryToDelete, "*.*", SearchOption.AllDirectories))
                {
                    if (FileLockInfo.FileUtil.WhoIsLocking(filename).Count > 0)
                        throw new Exception("File Locked");
                }

                Directory.Delete(directoryToDelete, true);
            }
            catch (Exception)
            {
                _toDestroy.Enqueue(directoryToDelete);
            }
        }
    }


    /// <summary>
    /// Stops the background cleanup timer, waits for any in-flight cleanup to finish,
    /// then destroys all remaining sessions and makes a best-effort attempt to delete
    /// their directories. Retries up to 10 times with a 500ms delay between attempts
    /// for directories that still have locked files.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return;

        CleanUpTimer.Stop();
        CleanUpTimer.Elapsed -= CleanUpTimer_Elapsed;
        CleanUpTimer.Dispose();

        // Wait for any in-flight timer callback to finish before we run our own cleanup.
        SpinWait spin = default;
        while (Interlocked.CompareExchange(ref _cleanUpTimerRunning, 1, 0) != 0)
        {
            spin.SpinOnce();
        }
        // _cleanUpTimerRunning is now 1, so no timer callback can enter.

        foreach (var kvp in _caches)
            DestroySession(kvp.Key);

        int attempts = 0;
        while (_toDestroy.Count > 0 && attempts < 10)
        {
            CleanUpFiles();
            if (_toDestroy.Count > 0)
            {
                attempts++;
                Thread.Sleep(500);
            }
        }
    }

    /// <summary>
    /// Removes a session from the active cache and queues its directory for
    /// background deletion. If the session does not exist, this is a no-op.
    /// </summary>
    /// <param name="sessionName">The session identifier returned by <see cref="CreateNewSession"/>.</param>
    public void DestroySession(string sessionName)
    {
        if (_caches.TryRemove(sessionName, out string cachePath))
        {
            _toDestroy.Enqueue(cachePath);
        }
    }
}