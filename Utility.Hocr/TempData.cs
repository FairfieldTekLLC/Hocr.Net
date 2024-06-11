using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Utility.Hocr;

public class TempData : IDisposable
{
    private static readonly Lazy<TempData> LazyInstance =
        new(CreateInstanceOfT, LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly Dictionary<string, string> _caches = new();

    private System.Timers.Timer CleanUpTimer;


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
                catch (Exception e)
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

    public static TempData Instance => LazyInstance.Value;


    public string CreateDirectory(string sessionName, string directoryName)
    {
        if (!_caches.ContainsKey(sessionName))
            throw new Exception("Invalid Session.");

        if (Directory.Exists(Path.Combine(_caches[sessionName], directoryName)))
            throw new Exception("Directory Exists.");

        Directory.CreateDirectory(Path.Combine(_caches[sessionName], directoryName));

        return Path.Combine(_caches[sessionName], directoryName);
    }

    private static TempData CreateInstanceOfT()
    {
        return Activator.CreateInstance(typeof(TempData), true) as TempData;
    }

    public string CreateNewSession()
    {
        string sessionName = Guid.NewGuid().ToString();

        if (string.IsNullOrEmpty(sessionName))
            throw new Exception("Session name cannot be empty!");

        if (_caches.ContainsKey(sessionName))
            throw new Exception("Session already exist!");

        Regex rgx = new("[^a-zA-Z0-9 -]");

        string newFolderName = rgx.Replace(sessionName, "");

        string originalName = newFolderName;

        int counter = 0;

        while (Directory.Exists(Path.Combine(TemporaryFilePath, newFolderName)))
        {
            counter++;
            newFolderName = originalName + "_" + counter;
        }

        try
        {
            Directory.CreateDirectory(Path.Combine(TemporaryFilePath, newFolderName));
        }
        catch (Exception)
        {
            throw new Exception("Cannot Create Session Folder.");
        }

        _caches.Add(sessionName, Path.Combine(TemporaryFilePath, newFolderName));
        return newFolderName;
    }

    public string CreateTempFile(string sessionName, string extensionWithDot, string folders = null)
    {
        if (!_caches.ContainsKey(sessionName))
            throw new Exception("Invalid Session");
        string newFile = Path.Combine(_caches[sessionName],
            Path.GetFileNameWithoutExtension(Path.GetRandomFileName()) + DateTime.Now.Second +
            DateTime.Now.Millisecond + extensionWithDot);
        return newFile;
    }



    private readonly ConcurrentQueue<String> _toDestroy = new();
    private bool _cleanUpTimerRunning = false;


    private void CleanUpTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
    {
        if (_cleanUpTimerRunning)
            return;

        _cleanUpTimerRunning = true;
        try

        {
            CleanUpFiles();
        }
        catch (Exception)
        {
            //
        }

        _cleanUpTimerRunning = false;
    }

    private void CleanUpFiles()
    {
        if (_toDestroy.TryDequeue(out string directoryToDelete))
            try
            {
                if (!Directory.Exists(directoryToDelete))
                    return;

                foreach (string filename in Directory.GetFiles(directoryToDelete, "*.*", SearchOption.AllDirectories))
                {
                    if (FileLockInfo.FileUtil.WhoIsLocking(filename).Count > 0)
                        throw new Exception("File Locked");
                    if (File.Exists(filename))
                        File.Delete(filename);
                }
                if (Directory.Exists(directoryToDelete))
                    Directory.Delete(directoryToDelete, true);
            }
            catch (Exception)
            {
                _toDestroy.Enqueue(directoryToDelete);
                throw;
            }
    }


    public void Dispose()
    {
        CleanUpTimer.Stop();
        CleanUpTimer.Dispose();
        foreach (string key in _caches.Keys.ToList())
            DestroySession(key);

        int attempts = 0;
        while (_toDestroy.Count > 0)
        {
            try
            {
                attempts++;
                Thread.SpinWait(5);
                CleanUpFiles();
            }
            catch (Exception e)
            {
                if (attempts > 10)
                    return;
            }
        }

    }

    public void DestroySession(string sessionName)
    {
        if (!_caches.ContainsKey(sessionName))
            return;
        if (!_toDestroy.Contains(_caches[sessionName]))
            _toDestroy.Enqueue(_caches[sessionName]);
        if (_caches.ContainsKey(sessionName))
            _caches.Remove(sessionName);
    }
}