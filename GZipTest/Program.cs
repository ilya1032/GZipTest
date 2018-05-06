using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.IO;
using System.IO.Compression;


namespace GZipTest
{
    static public class GZipTest
    {
        static string fileName;
        static int dataPortionToCompressIndex = 0;
        static int dataPortionToWriteIndex = 0;
        static int prevDataPortionToWriteIndex = -1;
        static int prevDataPortionToCompressIndex = -1;
        static Semaphore sem;

        static void Main(string[] args)
        {
            sem = new Semaphore(1, 1);

            fileName = "D:\\TestVideo.mkv";
            try
            {
            MyThreadPool pool = new MyThreadPool();
            }
            catch (TypeInitializationException ex) 
            {
                Console.WriteLine(ex.InnerException);
            }
            Compress(fileName);

            return;
        }

        public delegate void Compression();
        public delegate void Decompression();
        public delegate void WritingToFile();
        public static event Compression OnNewDataToCompress;
        static SortedList<int, byte[]> dataWaitingToCompress;
        static SortedList<int, byte[]> dataWaitingToWrite;

        public class MyThreadPool
        {
            public static FileStream outFile;
            static Queue<Thread> currentWorkingTreads;
            public static Thread outputThread;

            static int maxThreadsNumbers = (int)(Environment.WorkingSet / 1024*1024);    //потому что размер стека одного потока равен 1 мб
            public static int _dataPortionSize = 1042 * 1024;                           //(int)(Environment.WorkingSet / maxThreadsNumbers);//SystemPageSize вовращает размер доступной памяти для данного процесса

            public MyThreadPool()
            {
                OnNewDataToCompress += InitCompression;
                outFile = new FileStream(fileName + ".gz", FileMode.Append);
                dataWaitingToCompress = new SortedList<int, byte[]>();
                dataWaitingToWrite = new SortedList<int, byte[]>();
                currentWorkingTreads = new Queue<Thread>();
                outputThread = new Thread(new ThreadStart(WriteBlock));
                outputThread.Start();
            }

            public static bool QueueUserWorkItem(byte[] dataToCompress)
            {
                dataWaitingToCompress.Add(dataPortionToCompressIndex++,dataToCompress);
                OnNewDataToCompress();
                return true;
            }

            public static void InitCompression()
            {
                Thread t = new Thread(new ThreadStart(CompressBlock));
                t.Start();
                currentWorkingTreads.Enqueue(t);
            }

            public static int AvailableThreads()
            {
                return maxThreadsNumbers - currentWorkingTreads.Count;
            }
        }

        public static void CompressBlock()
        {
            byte[] dataPortion;
            sem.WaitOne();
            prevDataPortionToCompressIndex++;
            dataPortion = dataWaitingToCompress[prevDataPortionToCompressIndex];
            dataWaitingToCompress.Remove(prevDataPortionToCompressIndex);
            sem.Release();
                using (MemoryStream output = new MemoryStream(dataPortion.Length))
                {
                    using (GZipStream cs = new GZipStream(output, CompressionMode.Compress))
                    {
                        cs.Write(dataPortion, 0, dataPortion.Length);
                        sem.WaitOne();
                        dataWaitingToWrite.Add(dataPortionToWriteIndex++, output.ToArray());
                        sem.Release();
                    }
                }
        }

        public static void Compress(string inFileName)
        {
            FileStream inFile = new FileStream(inFileName, FileMode.Open);
            int _dataPortionSize;
            
            while (inFile.Position < inFile.Length)
            {
                if (inFile.Length - inFile.Position < MyThreadPool._dataPortionSize)
                {
                    _dataPortionSize = (int)(inFile.Length - inFile.Position);
                }
                else
                {
                    _dataPortionSize = MyThreadPool._dataPortionSize;
                }

                byte[] dataPortion = new byte[_dataPortionSize];
                inFile.Read(dataPortion, 0, _dataPortionSize);

                while (dataWaitingToWrite.Count + dataWaitingToCompress.Count >= 600)
                {
                    Thread.Sleep(300);
                }
                MyThreadPool.QueueUserWorkItem(dataPortion);    
            }

            inFile.Close();
        }

        public static void WriteBlock()
        {
            byte[] dataToWrite;
            int count = 0;
            while (count != 20)
            {
                while (!dataWaitingToWrite.ContainsKey(prevDataPortionToWriteIndex + 1) && (count != 20))
                {
                    Thread.Sleep(300);
                    count++;
                }
                if (count != 20)
                {
                    dataToWrite = dataWaitingToWrite[prevDataPortionToWriteIndex + 1];
                    prevDataPortionToWriteIndex++;
                    dataWaitingToWrite.Remove(prevDataPortionToWriteIndex);
                    MyThreadPool.outFile.Write(dataToWrite, 0, dataToWrite.Length);
                    count = 0;
                }
            }
        }
    }
    
}
