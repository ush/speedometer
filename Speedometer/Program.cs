using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Globalization;

using System.Data;
using System.Data.SqlClient;

public class Program
{
    public static long overallbytesNum = 0;
    public static long overallTimeMs = 0;
    static string DirPath = @"testdata";
    static int CycleNum = 25;
    static int OuterCycleNum = 50;
    static int ThreadsNum = 15;

    static string connectionString = GetConnectionString();

    static void Main(string[] args)
    {
        for (int i = 0, len = args.Length; i < len; i++)
        {
            var arg = args[i];
            var argLen = arg.Length;
            if (arg == "--")
            {
                break;
            }

            arg = arg.TrimStart('-');
            if (argLen == arg.Length)
            {
                break;
            }

            var flag = arg;
            switch (flag)
            {
                case "help":
                case "h":
                    Console.WriteLine("speedtest [-h] [-d dir] [-c inner cycles num] [-oc outer cycles num] [-t treads num]");
                    Console.WriteLine("");
                    Console.WriteLine("Output string:");
                    // формат строки вывода строго как указано ниже
                    Console.WriteLine("<computer name>,<operation type - Read/Write>,<thread number>,<cicle time (miliseconds)>,<read/writen (Mbytes)>,<cycle speed (Mbytes/sec)>,<current thread speed (Mbytes/sec)>,<overal speed (Mbytes/sec)>");

                    return;
                case "dir":
                case "d":
                    i++;
                    if (i >= len)
                    {
                        Console.Error.WriteLine("Expected -{flag} argument.");
                        Environment.Exit(1);
                        return;
                    }
                    DirPath = args[i];
                    continue;
                case "cycles":
                case "c":
                    i++;
                    if (i >= len)
                    {
                        Console.Error.WriteLine("Expected -{flag} argument.");
                        Environment.Exit(1);
                        return;
                    }
                    if (!Int32.TryParse(args[i], out CycleNum))
                    {
                        Console.Error.WriteLine("Cannot parse -{flag} argument.");
                        Environment.Exit(1);
                        return;
                    }
                    continue;
                case "outcycles":
                case "oc":
                    i++;
                    if (i >= len)
                    {
                        Console.Error.WriteLine("Expected -{flag} argument.");
                        Environment.Exit(1);
                        return;
                    }
                    if (!Int32.TryParse(args[i], out OuterCycleNum))
                    {
                        Console.Error.WriteLine("Cannot parse -{flag} argument.");
                        Environment.Exit(1);
                        return;
                    }
                    continue;
                case "threads":
                case "t":
                    i++;
                    if (i >= len)
                    {
                        Console.Error.WriteLine("Expected -{flag} argument.");
                        Environment.Exit(1);
                        return;
                    }
                    if (!Int32.TryParse(args[i], out ThreadsNum))
                    {
                        Console.Error.WriteLine("Cannot parse -{flag} argument.");
                        Environment.Exit(1);
                        return;
                    }
                    continue;
                case "database":
                case "db":
                    i++;
                    if (i >= len)
                    {
                        Console.Error.WriteLine("expected -{flag} argument.");
                        Environment.Exit(1);
                        return;
                    }

                    Console.Error.WriteLine("database result writting doesn't supported in this version.");
                    Environment.Exit(1);
                    continue;
                default:
                    Console.Error.WriteLine("Unknown option {flag}");
                    Environment.Exit(1);
                    return;
            }
        }

        double creationTime = 0;
        Console.WriteLine(DirPath);
        // Creating and filling the directory.
        if (!Directory.Exists(DirPath))
        {
            try
            {
                Directory.CreateDirectory(DirPath);
            }
            catch (DirectoryNotFoundException dirEx)
            {
                Console.WriteLine("An error occured: " + dirEx.Message);
                throw new DirectoryNotFoundException("Wrong folder path");
            }
            catch (IOException IOEx)
            {
                Console.WriteLine("An error occured: " + IOEx.Message);
                throw new IOException("Specified path is a file");
            }
        }

        if (IsDirectoryEmpty(DirPath))
        {
            creationTime = fillFolder(DirPath);
        }

        OuterCycleNum = (int)Math.Ceiling((double)OuterCycleNum / ThreadsNum);
        var pool = new Thread[ThreadsNum];
        DateTime startingTime = DateTime.Now;
        for (int i = 0; i < ThreadsNum; i++)
        {
            var tws = new ThreadState(ref DirPath, ref startingTime, ref overallbytesNum, ref CycleNum, ref OuterCycleNum);
            var t = new Thread(new ThreadStart(tws.ThreadProc));
            t.Name = "th" + i.ToString("D3");
            t.Start();
            pool[i] = t;
        }
        foreach (var t in pool)
        {
            t.Join();
        }

        DateTime endingTime = DateTime.Now;
        TimeSpan span = endingTime - startingTime;
        overallTimeMs = (long)(span.TotalSeconds * 1000);

        Console.WriteLine(overallbytesNum);
        Console.WriteLine(overallTimeMs);

        // только для целей отладки и просмотра вывода.
        Console.Write("Press <ESC> to exit... ");
        while (Console.ReadKey().Key != ConsoleKey.Escape) { }
    }

    public static void ShowResult(string iOperationType, string iThreadName, long iCicleTime, long iBytesProcessed, double iCileSpeed, double iCurrentThreadSpeed, double iOveralSpeed)
    {
        string ComputerName = Environment.MachineName;
        NumberFormatInfo nfi = new CultureInfo("en-US", false).NumberFormat;
        nfi.NumberDecimalSeparator = ".";
        Console.WriteLine(ComputerName + ", "
                           + iOperationType + ", "
                           + iThreadName + ", "
                           + iCicleTime.ToString() + ", "
                           + (iBytesProcessed / (1024 * 1024)).ToString() + ", "
                           + iCileSpeed.ToString("F2", nfi) + ", "
                           + iCurrentThreadSpeed.ToString("F2", nfi) + ", "
                           + iOveralSpeed.ToString("F2", nfi)
                           );
        string commandText = "INSERT INTO SpeedometerOutput VALUES ("
            + "'" + ComputerName + "'" + ", "
            + "'" + iOperationType + "'" + ", "
            + "'" + iThreadName + "'" + ", "
            + "'" + iCicleTime.ToString() + "'" + ", "
            + "'" + (iBytesProcessed / (1024 * 1024)).ToString() + "'" + ", "
            + "'" + iCileSpeed.ToString("F2", nfi) + "'" + ", "
            + "'" + iCurrentThreadSpeed.ToString("F2", nfi) + "'" + ", "
            + "'" + iOveralSpeed.ToString("F2", nfi) + "'"
            + ")";
        using (SqlConnection connection = new SqlConnection(connectionString)) {
              SqlCommand command = new SqlCommand(commandText, connection);
              command.Connection.Open();
              command.ExecuteNonQuery();
        }
    }

    static private string GetConnectionString() {
        return "Data Source = DETI-MB001\\SQLEXPRESS; Initial Catalog = Speedometer; Integrated Security = true;";
    }

    public static string GetRandomFile(ref FileInfo[] df, ref Random rnd)
    {
        int i = rnd.Next(0, df.Length - 1);
        return df[i].FullName;
    }

    static bool IsDirectoryEmpty(string DirPath)
    {
        return !Directory.EnumerateFileSystemEntries(DirPath).Any();
    }
    
    static double fillFolder(string DirPath)
    {
        var orig = DirPath;
        var data = new byte[5 * 1024 * 1024];
        var t1 = DateTime.Now;
        for (var i = 0; i < 1000; i++)
        {
            DirPath = orig + "/File" + i;
            File.WriteAllBytes(DirPath, data);
        }
        var t2 = DateTime.Now;
        TimeSpan sp = t2 - t1;
        return sp.TotalSeconds;
    }
}

