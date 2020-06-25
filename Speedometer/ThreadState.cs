using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Globalization;

public class ThreadState
{
    public string path;
    public int cycleNum;
    public int outerCycleNum;
    public long bytes = 0;
    public long time = 0;
    public DateTime startingTime;

    public ThreadState(ref string dirPath, ref DateTime stTime, ref long bytesNum, ref int cNum, ref int oCNum)
    {
        path = dirPath;
        cycleNum = cNum;
        outerCycleNum = oCNum;
        bytes = bytesNum;
        time = 0;
        startingTime = stTime;
    }

    public void ThreadProc()
    {
        long overallTimeSpan = 0;
        Mutex mut = new Mutex();
        var th = Thread.CurrentThread.ManagedThreadId;
        var thName = Thread.CurrentThread.Name;

        DirectoryInfo di = new DirectoryInfo(path);
        FileInfo[] df = null;
        for (int j = 0; j < outerCycleNum; j++)
        {
            try
            {
                df = di.GetFiles();
            }
            catch (DirectoryNotFoundException dirEx)
            {
                // Let the user know that the directory did not exist.
                Console.Error.WriteLine("An error occured: " + dirEx.Message);
                throw new DirectoryNotFoundException("Wrong folder path");
            }
            Random rnd = new Random();
            // int[] seen;

            long testTime = 0;
            long testBytesNum = 0;
            for (int i = 0; i < cycleNum; i++)
            {
                //getting the info about the random file
                string randomFile = Program.GetRandomFile(ref df, ref rnd);
                FileInfo fi = new FileInfo(randomFile);

                //calculating the time required to read this file
                var t1 = DateTime.Now;
                File.ReadAllText(randomFile);
                var t2 = DateTime.Now;
                TimeSpan sp = t2 - t1;

                testTime += (long)(sp.TotalSeconds * 1000);
                testBytesNum += fi.Length;
            }
            var time2 = DateTime.Now;
            TimeSpan span = time2 - startingTime;
            overallTimeSpan = (long)(span.TotalSeconds * 1000);

            time += testTime;
            bytes += testBytesNum;

            Program.overallbytesNum += bytes;
            
            //ShowResults(ref cycleTime, ref bytesNum, ref th);

            Program.ShowResult("READ",
                thName,
                testTime,
                testBytesNum,
                ((double)((testBytesNum * 1000) / testTime) / (1024 * 1024)),
                ((double)((bytes * 1000) / time) / (1024 * 1024)),
                ((double)((Program.overallbytesNum * 1000) / overallTimeSpan) / (1024 * 1024))
                );

            mut.WaitOne();
            mut.ReleaseMutex();

        }
    }
}

