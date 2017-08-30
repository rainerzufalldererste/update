using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Microsoft.WindowsAPICodePack.Taskbar;

namespace update
{
  static class Program
  {
    static string ToPath(this string path)
    {
      if (path.Contains('\\'))
        path = path.Replace('\\', '/');

      if (!path.EndsWith("/"))
        path += "/";

      return path;
    }

    enum EUpdateMode
    {
      update, revert, no_new_files
    }

    enum EOtherArgs
    {
      init, status, change, copy
    }

    static string CurrentDirectory = null;
    static EUpdateMode UpdateMode = EUpdateMode.update;
    static ulong totalFiles = 0;
    static ulong totalBytes = 0;
    static bool diff = false;
    static bool verbose = false;
    static bool subdir = false;
    static bool porcelain = false;

    static void Main(string[] args)
    {
      if (args.Length != 0)
      {
        string arg = args[0].ToLower();

        if (arg == EUpdateMode.update.ToString())
        {
          UpdateMode = EUpdateMode.update;
        }
        else if (arg == EUpdateMode.revert.ToString())
        {
          UpdateMode = EUpdateMode.revert;
        }
        else if (arg == EUpdateMode.no_new_files.ToString())
        {
          UpdateMode = EUpdateMode.no_new_files;
        }
        else if (arg == EOtherArgs.status.ToString())
        {
          string currentDirectory = FindDirectoryUpwards(Environment.CurrentDirectory);

          if (currentDirectory == null)
          {
            Console.WriteLine("No .update file found.");
            Environment.Exit(-20);
          }
          else
          {
            Console.WriteLine($"The current .update file directory is {currentDirectory}.");

            string[] contents = ReadUpdateFile(currentDirectory);

            if(contents.Length > 0)
            {
              Console.WriteLine($"The directory to update from is {contents[0]}.");
            }
            else
            {
              Console.WriteLine($"The .update file is invalid and contains no data.\nAborting.");
              Environment.Exit(-22);
            }
          }
          return;
        }
        else if (arg == EOtherArgs.change.ToString())
        {
          string directory;

          if (args.Length < 2)
          {
            Console.WriteLine("Enter the path to update form");
            directory = Console.ReadLine().ToPath();
          }
          else
          {
            directory = args[1].ToPath();
          }

          if (File.Exists(Environment.CurrentDirectory.ToPath() + ".update"))
          {
            Console.WriteLine("Removing original .update file of this directory.");
            File.Delete(Environment.CurrentDirectory.ToPath() + ".update");
          }

          CreateUpdateFile(Environment.CurrentDirectory, directory);

          return;
        }
        else if (arg == EOtherArgs.init.ToString())
        {
          string directory;

          if (args.Length < 2)
          {
            Console.WriteLine("Enter the path to update form");
            directory = Console.ReadLine().ToPath();
          }
          else
          {
            directory = args[1].ToPath();
          }

          if (File.Exists(Environment.CurrentDirectory.ToPath() + ".update"))
          {
            Console.WriteLine("This directory already contains a .update file.\nAborting.");
            Environment.Exit(-21);
            return;
          }

          CreateUpdateFile(Environment.CurrentDirectory, directory);

          return;
        }
        else if (arg == EOtherArgs.copy.ToString())
        {
          if (args.Length > 2)
          {
            string arg_ = args[2].ToLower();

            if (arg_ == EUpdateMode.update.ToString())
            {
              UpdateMode = EUpdateMode.update;
            }
            else if (arg_ == EUpdateMode.revert.ToString())
            {
              UpdateMode = EUpdateMode.revert;
            }
            else if (arg_ == EUpdateMode.no_new_files.ToString())
            {
              UpdateMode = EUpdateMode.no_new_files;
            }
            else
            {
              Console.WriteLine($"Invalid Update mode '{arg_}'.\nAborting.");
              Environment.Exit(-22);
              return;
            }
          }

          string srcDirectory = args[1].Trim('"');

          UpdateDirectory(Environment.CurrentDirectory, srcDirectory);

          return;
        }
        else
        {
          Console.WriteLine($"Possible Arguments:\nupdate ({EUpdateMode.update} | {EUpdateMode.revert} | {EUpdateMode.no_new_files}) ({nameof(diff)}, {nameof(verbose)}, {nameof(subdir)}, {nameof(porcelain)})\nupdate {EOtherArgs.init} <path>\nupdate {EOtherArgs.change} <path>\nupdate {EOtherArgs.copy} <source path>\nupdate {EOtherArgs.status}");
          return;
        }

        if (args.Length > 1)
        {
          for (int i = 1; i < args.Length; i++)
          {
            if (args[i] == nameof(diff))
            {
              diff = true;
              verbose = true;
            }
            else if (args[i] == nameof(verbose))
            {
              verbose = true;
            }
            else if (args[i] == nameof(subdir))
            {
              subdir = true;
            }
            else if (args[i] == nameof(porcelain))
            {
              porcelain = true;
            }
            else
            {
              Console.WriteLine($"Invalid Parameter {args[i]}.\nAborting.");
              Environment.Exit(-100);
            }
          }
        }
      }

      try
      {
        CurrentDirectory = FindDirectoryUpwards(Environment.CurrentDirectory);
      }
      catch (Exception e)
      {
        Console.WriteLine($"An error occured when finding the .update file:\n{e}\n\nAborting.");
        Environment.Exit(-1);
      }

      if (CurrentDirectory == null)
      {
        Console.WriteLine("Couldn't find .update File.");

        CreateUpdateFile(Environment.CurrentDirectory);
      }
      else
      {
        if (!File.Exists(CurrentDirectory + ".update"))
        {
          Console.WriteLine("Couldn't find .update File. It was probably just removed.\nAborting.");
          Environment.Exit(-3);
        }

        string[] contents = ReadUpdateFile(CurrentDirectory);
        string updateDirectory = contents[0];

        UpdateDirectory(CurrentDirectory, updateDirectory);
      }
    }

    private static void UpdateDirectory(string currentDirectory, string updateDirectory)
    {
      updateDirectory = updateDirectory.ToPath();
      currentDirectory = currentDirectory.ToPath();

      if(subdir)
      {
        string subPath = Environment.CurrentDirectory.ToPath().Substring(currentDirectory.Length);
        updateDirectory += subPath;

        if(!Directory.Exists(updateDirectory))
        {
          Console.WriteLine($"Subpath '{subPath}' does not exist in the update directory.\nAborting.");
          Environment.Exit(-100);
        }

        currentDirectory += subPath;
        System.Diagnostics.Debug.Assert(currentDirectory == Environment.CurrentDirectory.ToPath(), "The new update path should be equal to the WorkingDirectory.");
      }

      string[] updateFiles;
      List<string> currentFiles;

      try
      {
        Console.WriteLine("Reading Current...");
        currentFiles = Directory.GetFiles(currentDirectory, "*", SearchOption.AllDirectories).OrderByDescending(s => s).ToList();
      }
      catch (Exception e)
      {
        Console.WriteLine($"An error occured when reading the current Directory:\n{e}\n\nAborting.");
        Environment.Exit(-7);
        return; // just for compiler errors ;)
      }

      try
      {
        Console.WriteLine("Reading Update...");
        updateFiles = Directory.GetFiles(updateDirectory, "*", SearchOption.AllDirectories).OrderByDescending(s => s).ToArray();
      }
      catch (Exception e)
      {
        Console.WriteLine($"An error occured when reading the update Directory:\n{e}\n\nAborting.");
        Environment.Exit(-8);
        return; // just for compiler errors ;)
      }

      Console.WriteLine("Processing files...");

      for (int i = 0; i < updateFiles.Length; i++)
        updateFiles[i] = updateFiles[i].Substring(updateDirectory.Length).Replace('\\', '/');

      for (int i = 0; i < currentFiles.Count; i++)
        currentFiles[i] = currentFiles[i].Substring(currentDirectory.Length).Replace('\\', '/');

      Console.WriteLine("Updating files...");

      switch (UpdateMode)
      {
        case EUpdateMode.revert:
          UpdateRevert(updateFiles, ref currentFiles, updateDirectory);
          break;

        case EUpdateMode.update:
          UpdateUpdate(updateFiles, ref currentFiles, updateDirectory);
          break;

        case EUpdateMode.no_new_files:
          UpdateNoNewFiles(updateFiles, ref currentFiles, updateDirectory);
          break;

        default:
          Console.WriteLine("Invalid UpdateMode.\nAborting.");
          Environment.Exit(-10);
          return;
      }

      ClearWrite("Done.");

      byte unitIndex = 0;
      double bytesRelativeToUnit = totalBytes;

      while(bytesRelativeToUnit > 1024.0 && unitIndex < 8)
      {
        unitIndex++;
        bytesRelativeToUnit /= 1024;
      }

      string[] units = { "Bytes", "KiB", "MiB", "GiB", "TiB", "PiB", "EiB", "ZiB", "YiB" };

      ClearWrite($"Updated {totalFiles} Files ({bytesRelativeToUnit.ToString("0.00")} {units[unitIndex]}).");
    }


    private static string[] ReadUpdateFile(string currentDirectory)
    {
      string[] ret = null;

      try
      {
        ret = File.ReadAllLines(currentDirectory + ".update");
      }
      catch (Exception e)
      {
        Console.WriteLine($"An error occured when reading the .update file:\n{e}\n\nAborting.");
        Environment.Exit(-4);
        throw new Exception(); // just for compiler errors ;)
      }

      if (ret.Length == 0)
      {
        Console.WriteLine($"An error occured when reading the .update file:\nThe length was 0.\n\nAborting.");
        Environment.Exit(-5);
        throw new Exception(); // just for compiler errors ;)
      }

      if (!Directory.Exists(ret[0]))
      {
        Console.WriteLine($"An error occured when reading the .update file:\nThe given directory '{ret[0]}' does not exist or is not accessable.\n\nAborting.");
        Environment.Exit(-6);
        throw new Exception(); // just for compiler errors ;)
      }

      return ret;
    }

    private static void UpdateRevert(string[] updateFiles, ref List<string> currentFiles, string updateDirectory)
    {
      int count = currentFiles.Count;
      int index = 0;

      foreach (string file in currentFiles)
      {
        Console.Write("\rDeleting... (" + ((index / (float)count) * 100f).ToString("0.00") + "%)");

        if (!updateFiles.Contains(file) && !file.EndsWith(".update"))
        {
          bool deleted = true;

          if (!diff)
            deleted = DeleteFileIfExists(CurrentDirectory + file);

          if (verbose)
            ClearWrite($"Deleted {file}.", ConsoleColor.Red);
        }

        index++;
      }

      FileInfo current, update = null;

      count = updateFiles.Length;
      index = 0;
      int progressBarSteps = Console.BufferWidth - 1;

      bool taskbarFailed = false;

      try
      {
        TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Normal);
        TaskbarManager.Instance.SetProgressValue(index, count);
      }
      catch { taskbarFailed = true; }

      foreach (string file in updateFiles)
      {
        Console.Write("\r" + new string('▓', (int)(((float)index / (float)count) * (float)(progressBarSteps))) + new string('▒', (int)(((float)(count - index) / (float)count) * (float)(progressBarSteps))));

        if (!taskbarFailed)
          TaskbarManager.Instance.SetProgressValue(index, count);

        index++;

        if (currentFiles.Contains(file))
        {
          currentFiles.Remove(file);

          try
          {
            current = new FileInfo(CurrentDirectory + file);
            update = new FileInfo(updateDirectory + file);

            if (current.LastWriteTime.Equals(update.LastWriteTime) && current.Length == update.Length)
              continue;
          }
          catch (Exception e)
          {
            ClearWrite($"Failed read on file {file}. ({e.Message})", ConsoleColor.DarkRed);

            if (porcelain)
              Environment.Exit(-101);
          }
        }

        try
        {
          if (file.EndsWith(".update"))
            continue;

          bool deleted = false;

          if (!diff)
            deleted = CopyFile(updateDirectory + file, CurrentDirectory + file);

          totalFiles++;
          totalBytes += (ulong)new FileInfo(updateDirectory + file).Length;

          if (verbose)
          {
            if (deleted)
              ClearWrite($"Updated {file}.", ConsoleColor.Yellow);
            else
              ClearWrite($"Created {file}.", ConsoleColor.Green);
          }
        }
        catch (Exception e)
        {
          ClearWrite($"Failed to update {file}! ({e.Message})", ConsoleColor.DarkRed);

          if (porcelain)
            Environment.Exit(-101);
        }
      }

      if (!taskbarFailed)
        TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.NoProgress);
    }

    private static void UpdateUpdate(string[] updateFiles, ref List<string> currentFiles, string updateDirectory)
    {
      int count = updateFiles.Length;
      int index = 0;
      int progressBarSteps = Console.BufferWidth - 1;

      bool taskbarFailed = false;

      try
      {
        TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Normal);
        TaskbarManager.Instance.SetProgressValue(index, count);
      }
      catch { taskbarFailed = true; }

      foreach (string file in updateFiles)
      {
        Console.Write("\r" + new string('▓', (int)(((float)index / (float)count) * (float)(progressBarSteps))) + new string('▒', (int)(((float)(count - index) / (float)count) * (float)(progressBarSteps))));

        if (!taskbarFailed)
          TaskbarManager.Instance.SetProgressValue(index, count);

        index++;

        if (currentFiles.Contains(file))
        {
          currentFiles.Remove(file);

          try
          {
            FileInfo current = new FileInfo(CurrentDirectory + file);
            FileInfo update = new FileInfo(updateDirectory + file);

            if (current.LastWriteTime.Equals(update.LastWriteTime) && current.Length == update.Length)
              continue;

            if (current.LastWriteTime > update.LastWriteTime)
              continue;
          }
          catch (Exception e)
          {
            ClearWrite($"Failed read on file {file}. ({e.Message})", ConsoleColor.DarkRed);

            if (porcelain)
              Environment.Exit(-101);
          }
        }

        try
        {
          if (file.EndsWith(".update"))
            continue;

          bool deleted = false;

          if (!diff)
            deleted = CopyFile(updateDirectory + file, CurrentDirectory + file);

          totalFiles++;
          totalBytes += (ulong)new FileInfo(updateDirectory + file).Length;

          if (verbose)
          {
            if (deleted)
              ClearWrite($"Updated {file}.", ConsoleColor.Yellow);
            else
              ClearWrite($"Created {file}.", ConsoleColor.Green);
          }
        }
        catch (Exception e)
        {
          ClearWrite($"Failed to update {file}! ({e.Message})", ConsoleColor.DarkRed);

          if (porcelain)
            Environment.Exit(-101);
        }
      }

      if (!taskbarFailed)
        TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.NoProgress);
    }


    private static void UpdateNoNewFiles(string[] updateFiles, ref List<string> currentFiles, string updateDirectory)
    {
      int count = updateFiles.Length;
      int index = 0;
      int progressBarSteps = Console.BufferWidth - 1;

      bool taskbarFailed = false;

      try
      {
        TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Normal);
        TaskbarManager.Instance.SetProgressValue(index, count);
      }
      catch { taskbarFailed = true; }

      foreach (string file in updateFiles)
      {
        Console.Write("\r" + new string('▓', (int)(((float)index / (float)count) * (float)(progressBarSteps))) + new string('▒', (int)(((float)(count - index) / (float)count) * (float)(progressBarSteps))));

        if(!taskbarFailed)
          TaskbarManager.Instance.SetProgressValue(index, count);

        index++;

        if (currentFiles.Contains(file))
        {
          currentFiles.Remove(file);

          try
          {
            FileInfo current = new FileInfo(CurrentDirectory + file);
            FileInfo update = new FileInfo(updateDirectory + file);

            if (current.LastWriteTime.Equals(update.LastWriteTime) && current.Length == update.Length)
              continue;

            if (current.LastWriteTime > update.LastWriteTime)
              continue;
          }
          catch (Exception e)
          {
            ClearWrite($"Failed read on file {file}. ({e.Message})", ConsoleColor.DarkRed);

            if (porcelain)
              Environment.Exit(-101);
          }
        }
        else
        {
          continue;
        }

        try
        {
          if (file.EndsWith(".update"))
            continue;
          bool deleted = false;

          if (!diff)
            deleted = CopyFile(updateDirectory + file, CurrentDirectory + file);

          totalFiles++;
          totalBytes += (ulong)new FileInfo(updateDirectory + file).Length;

          if (verbose)
          {
            if (deleted)
              ClearWrite($"Updated {file}.", ConsoleColor.Yellow);
            else
              ClearWrite($"Created {file}.", ConsoleColor.Green);
          }
        }
        catch (Exception e)
        {
          ClearWrite($"Failed to update {file}! ({e.Message})", ConsoleColor.DarkRed);

          if (porcelain)
            Environment.Exit(-101);
        }
      }

      if (!taskbarFailed)
        TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.NoProgress);
    }


    private static void CreateUpdateFile(string currentDirectory, string updateDirectory = null)
    {
      if (updateDirectory == null)
      {
        Console.WriteLine("Enter directory to update from:");
        updateDirectory = Console.ReadLine();
      }

      updateDirectory = updateDirectory.ToPath();
      currentDirectory = currentDirectory.ToPath();

      if (!Directory.Exists(updateDirectory))
      {
        Console.WriteLine("The given directory does not exist.\nAborting.");
        Environment.Exit(-2);
      }

      try
      {
        File.WriteAllText(currentDirectory + ".update", updateDirectory);
        Console.WriteLine($"Created .update file in {currentDirectory}");
      }
      catch (Exception e)
      {
        Console.WriteLine($"Failed to create .update file\n{e}\n\nAborting.");
        Environment.Exit(-9);
      }
    }

    private static string FindDirectoryUpwards(string currentDirectory)
    {
      if (currentDirectory.Contains('\\'))
        currentDirectory = currentDirectory.Replace('\\', '/');

      if (!currentDirectory.EndsWith("/"))
        currentDirectory += "/";

      if (Directory.GetFiles(currentDirectory, "*", SearchOption.TopDirectoryOnly).Contains(currentDirectory + ".update"))
      {
        return currentDirectory;
      }
      else
      {
        for (int i = currentDirectory.Length - 2; i >= 0; i--)
        {
          if (currentDirectory[i] == '/')
          {
            currentDirectory = currentDirectory.Remove(i + 1);
            return FindDirectoryUpwards(currentDirectory);
          }
        }
      }

      return null;
    }

    private static bool DeleteFileIfExists(string path)
    {
      try
      {
        if (File.Exists(path))
        {
          File.Delete(path);
          return true;
        }

        return false;
      }
      catch (Exception e)
      {
        ClearWrite($"Failed to remove file {path}. ({e.Message})", ConsoleColor.DarkRed);

        if (porcelain)
          Environment.Exit(-101);

        return false;
      }
    }

    private static bool CopyFile(string sourcePath, string destinationPath)
    {
      try
      {
        string dir = destinationPath;

        for (int i = dir.Length - 2; i >= 0; i--)
        {
          if (dir[i] == '/')
          {
            dir = dir.Remove(i + 1);

            if (!Directory.Exists(dir))
            {
              if (!diff)
                Directory.CreateDirectory(dir);
            }

            break;
          }
        }
      }
      catch (Exception e)
      {
        ClearWrite($"Failed to create directory for {destinationPath}. ({e.Message})", ConsoleColor.DarkRed);

        if (porcelain)
          Environment.Exit(-101);
      }

      bool ret = DeleteFileIfExists(destinationPath);

      try
      {
        File.Copy(sourcePath, destinationPath);
      }
      catch (Exception e)
      {
        ClearWrite($"Failed to write file {sourcePath}. ({e.Message})", ConsoleColor.DarkRed);
        ret = false;

        if (porcelain)
          Environment.Exit(-101);
      }

      return ret;
    }

    public static void ClearWrite(string s)
    {
      Console.Write("\r" + s + new string(' ', Console.WindowWidth - 1 - (s.Length % Console.WindowWidth)) + "\n");
    }

    public static void ClearWrite(string s, ConsoleColor color)
    {
      Console.ForegroundColor = color;
      ClearWrite(s);
      Console.ResetColor();
    }
  }
}
