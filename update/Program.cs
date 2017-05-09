using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace update
{
  class Program
  {
    enum EUpdateMode
    {
      update, revert, no_new_files
    }

    static string CurrentDirectory = null;
    static EUpdateMode UpdateMode = EUpdateMode.update;
    static ulong totalFiles = 0;
    static ulong totalBytes = 0;


    static void Main(string[] args)
    {
      if(args.Length != 0)
      {
        if (args[0].ToLower() == EUpdateMode.update.ToString())
        {
          UpdateMode = EUpdateMode.update;
        }
        else if (args[0].ToLower() == EUpdateMode.revert.ToString())
        {
          UpdateMode = EUpdateMode.revert;
        }
        else if (args[0].ToLower() == EUpdateMode.no_new_files.ToString())
        {
          UpdateMode = EUpdateMode.no_new_files;
        }
        else
        {
          Console.WriteLine($"Possible Arguments:\n{EUpdateMode.update},{EUpdateMode.revert},{EUpdateMode.no_new_files}\n");
          return;
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

        string[] contents;

        try
        {
          contents = File.ReadAllLines(CurrentDirectory + ".update");
        }
        catch (Exception e)
        {
          Console.WriteLine($"An error occured when reading the .update file:\n{e}\n\nAborting.");
          Environment.Exit(-4);
          return; // just for compiler errors ;)
        }

        if (contents.Length == 0)
        {
          Console.WriteLine($"An error occured when reading the .update file:\nThe length was 0.\n\nAborting.");
          Environment.Exit(-5);
          return; // just for compiler errors ;)
        }

        if (!Directory.Exists(contents[0]))
        {
          Console.WriteLine($"An error occured when reading the .update file:\nThe given directory does not exist or is not accessable.\n\nAborting.");
          Environment.Exit(-6);
          return; // just for compiler errors ;)
        }

        string updateDirectory = contents[0];

        if (updateDirectory.Contains('\\'))
          updateDirectory = updateDirectory.Replace('\\', '/');

        if (!updateDirectory.EndsWith("/"))
          updateDirectory += "/";

        if (CurrentDirectory.Contains('\\'))
          CurrentDirectory = CurrentDirectory.Replace('\\', '/');

        if (!CurrentDirectory.EndsWith("/"))
          CurrentDirectory += "/";

        string[] updateFiles;
        List<string> currentFiles;

        try
        {
          Console.WriteLine("Reading Current...");
          currentFiles = Directory.GetFiles(CurrentDirectory, "*", SearchOption.AllDirectories).OrderByDescending(s => s).ToList();
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
          currentFiles[i] = currentFiles[i].Substring(CurrentDirectory.Length).Replace('\\', '/');

        Console.WriteLine("Updating files...");

        switch(UpdateMode)
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
            Console.WriteLine("Invalid EUpdateMode.\nAborting.");
            Environment.Exit(-10);
            return;
        }

        Console.WriteLine($"Done.\nUpdated {totalFiles} Files ({totalBytes} Bytes).");
      }
    }

    private static void UpdateRevert(string[] updateFiles, ref List<string> currentFiles, string updateDirectory)
    {
      foreach (string file in currentFiles)
      {
        if (!updateFiles.Contains(file) && !file.EndsWith(".update"))
        {
          File.Delete(CurrentDirectory + file);
          Console.WriteLine($"Deleted {file}.");
        }
      }

      FileInfo current, update = null;

      foreach (string file in updateFiles)
      {
        if (currentFiles.Contains(file))
        {
          currentFiles.Remove(file);

          try
          {
            current = new FileInfo(CurrentDirectory + file);
            update =  new FileInfo(updateDirectory + file);

            if (current.LastWriteTime.Equals(update.LastWriteTime) && current.Length == update.Length)
              continue;
          }
          catch
          {
            Console.WriteLine($"Failed read on file {file}.");
          }
        }

        try
        {
          // get directory
          string dir = CurrentDirectory + file;

          for (int i = dir.Length - 2; i >= 0; i--)
          {
            if (dir[i] == '/')
            {
              dir = dir.Remove(i + 1);

              if (!Directory.Exists(dir))
              {
                Directory.CreateDirectory(dir);
              }
              else
              {
                break;
              }
            }
          }

          File.Delete(CurrentDirectory + file);
          File.Copy(updateDirectory + file, CurrentDirectory + file);

          totalFiles++;
          totalBytes += (ulong)new FileInfo(updateDirectory + file).Length;
          Console.WriteLine($"Updated {file}.");
        }
        catch (Exception e)
        {
          Console.WriteLine($"Failed to update {file}! ({e.Message})");
        }
      }
    }

    private static void UpdateUpdate(string[] updateFiles, ref List<string> currentFiles, string updateDirectory)
    {
      foreach (string file in updateFiles)
      {
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
          catch
          {
            Console.WriteLine($"Failed read on file {file}.");
          }
        }

        try
        {
          // get directory
          string dir = CurrentDirectory + file;

          for (int i = dir.Length - 2; i >= 0; i--)
          {
            if (dir[i] == '/')
            {
              dir = dir.Remove(i + 1);

              if (!Directory.Exists(dir))
              {
                Directory.CreateDirectory(dir);
              }
              else
              {
                break;
              }
            }
          }

          File.Delete(CurrentDirectory + file);
          File.Copy(updateDirectory + file, CurrentDirectory + file);

          totalFiles++;
          totalBytes += (ulong)new FileInfo(updateDirectory + file).Length;
          Console.WriteLine($"Updated {file}.");
        }
        catch (Exception e)
        {
          Console.WriteLine($"Failed to update {file}! ({e.Message})");
        }
      }
    }


    private static void UpdateNoNewFiles(string[] updateFiles, ref List<string> currentFiles, string updateDirectory)
    {
      foreach (string file in updateFiles)
      {
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
          catch
          {
            Console.WriteLine($"Failed read on file {file}.");
          }
        }
        else
        {
          continue;
        }

        try
        {
          // get directory
          string dir = CurrentDirectory + file;

          for (int i = dir.Length - 2; i >= 0; i--)
          {
            if (dir[i] == '/')
            {
              dir = dir.Remove(i + 1);

              if (!Directory.Exists(dir))
              {
                Directory.CreateDirectory(dir);
              }
              else
              {
                break;
              }
            }
          }

          File.Delete(CurrentDirectory + file);
          File.Copy(updateDirectory + file, CurrentDirectory + file);

          totalFiles++;
          totalBytes += (ulong)new FileInfo(updateDirectory + file).Length;
          Console.WriteLine($"Updated {file}.");
        }
        catch (Exception e)
        {
          Console.WriteLine($"Failed to update {file}! ({e.Message})");
        }
      }
    }


    private static void CreateUpdateFile(string currentDirectory)
    {
      Console.WriteLine("Enter directory to update from:");
      string updateDirectory = Console.ReadLine();

      if (currentDirectory.Contains('\\'))
        currentDirectory = currentDirectory.Replace('\\', '/');

      if (!currentDirectory.EndsWith("/"))
        currentDirectory += "/";

      if (updateDirectory.Contains('\\'))
        updateDirectory = updateDirectory.Replace('\\', '/');

      if (!updateDirectory.EndsWith("/"))
        updateDirectory += "/";

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
  }
}
