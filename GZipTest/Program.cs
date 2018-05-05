using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.IO;
using System.IO.Compression;


namespace GZipTest
{
    static public class GZipTest
    {

        static int dataPortionSize = 10000000;
/*
        static int threadNumber = Environment.ProcessorCount;

        //static Thread[] tPool = new Thread[threadNumber];

        static byte[][] dataArray = new byte[threadNumber][];
        static byte[][] compressedDataArray = new byte[threadNumber][];

        static int dataArraySize = dataPortionSize * threadNumber;
*/
        /*
                static public void Compress(string inFileName)
                {

                    FileStream inFile = new FileStream(inFileName, FileMode.Open);
                    FileStream outFile = new FileStream(inFileName + ".gz", FileMode.Append);
                    int _dataPortionSize;
                    Thread[] tPool;
                    Console.Write("Compressing...");
                    while (inFile.Position < inFile.Length)
                    {
                        Console.Write(".");
                        tPool = new Thread[threadNumber];
                        for (int portionCount = 0; (portionCount < threadNumber) && (inFile.Position < inFile.Length); portionCount++)
                        {
                            if (inFile.Length - inFile.Position <= dataPortionSize)
                            {
                                _dataPortionSize = (int)(inFile.Length - inFile.Position);
                            }
                            else
                            {
                                _dataPortionSize = dataPortionSize;
                            }
                            dataArray[portionCount] = new byte[_dataPortionSize];
                            inFile.Read(dataArray[portionCount], 0, _dataPortionSize);

                            tPool[portionCount] = new Thread(CompressBlock);
                            tPool[portionCount].Start(portionCount);
                        }

                        for (int portionCount = 0; (portionCount < threadNumber) && (tPool[portionCount] != null);)
                        {
                            if (tPool[portionCount].ThreadState == ThreadState.Stopped)
                            {
                                outFile.Write(compressedDataArray[portionCount], 0, compressedDataArray[portionCount].Length);
                                portionCount++;
                            }
                        }
                    }

                    outFile.Close();
                    inFile.Close();
                }

                static public void CompressBlock(object i)
                {
                    using (MemoryStream output = new MemoryStream(dataArray[(int)i].Length))
                    {
                        using (GZipStream cs = new GZipStream(output, CompressionMode.Compress))
                        {
                            cs.Write(dataArray[(int)i], 0, dataArray[(int)i].Length);
                        }
                        compressedDataArray[(int)i] = output.ToArray();
                    }
                }*/
        static void Main(string[] args)
        {

            string fileName = "D:\\original.jpg";

//          Compress(fileName);
        }

        public delegate void Compression();
        public delegate void Decompression();
        public delegate void WritingToFile();
        public static event WritingToFile OnNewDataToWrite;
        public static event Decompression OnNewDataToDecompress;
        public static event Compression OnNewDataToCompress;
        static Queue<byte[]> dataWaitingToCompress;
        static Queue<byte[]> dataWaitingToWrite;

        public class ThreadPool
        {
            static Queue<Thread> currentWorkingTreads;
            static Thread outputThread;

            static int maxThreadsNumbers = Environment.SystemPageSize / (1024*1024);    //потому что размер стека одного потока равен 1 мб
            public static int _dataPortionSize = Environment.SystemPageSize / maxThreadsNumbers;//SystemPageSize вовращает размер доступной памяти для данного процесса

            public ThreadPool()
            {
                OnNewDataToCompress += InitCompression;
                OnNewDataToDecompress += InitDecompression;
                OnNewDataToWrite += InitWriting;
            }

            public static bool QueueUserWorkItem(byte[] dataToCompress)
            {
                dataWaitingToCompress.Enqueue(dataToCompress);
                OnNewDataToCompress();
                return true;
            }

            public static void InitCompression()
            {
                Thread t = new Thread(new ThreadStart(CompressBlock));
                currentWorkingTreads.Enqueue(t);
            }

            public static void InitDecompression()
            {
                Thread t = new Thread(new ThreadStart(DecompressBlock));
                currentWorkingTreads.Enqueue(t);
            }
            //дописать
            public static void InitWriting()
            {
                if (outputThread == null)
                {
                    outputThread = new Thread(new ThreadStart(WriteBlock));
                    currentWorkingTreads.Enqueue(outputThread);
                }
                else
                {
                    while (outputThread.ThreadState != ThreadState.Stopped)
                        Thread.Sleep(300);
                    outputThread.Start();
                }
            }

            public static int AvailableThreads()
            {
                return maxThreadsNumbers - currentWorkingTreads.Count;
            }
        }

        public static void CompressBlock()
        {
            Object lockObject = new Object();
            byte[] dataPortion;
            lock (lockObject)
            {
                dataPortion = dataWaitingToCompress.Dequeue();
            }
                using (MemoryStream output = new MemoryStream(dataPortion.Length))
                {
                    using (GZipStream cs = new GZipStream(output, CompressionMode.Compress))
                    {
                        cs.Write(dataPortion, 0, dataPortion.Length);
                    }
                    lock (lockObject)
                    {
                        dataWaitingToWrite.Enqueue(output.ToArray());
                        OnNewDataToWrite();
                    }
                }
        }

        public static void Compress(string inFileName)
        {
            FileStream inFile = new FileStream(inFileName, FileMode.Open);
            FileStream outFile = new FileStream(inFileName + ".gz", FileMode.Append);
            int _dataPortionSize;
            
            while (inFile.Position <= inFile.Length)
            {
                if (inFile.Length - inFile.Position <= ThreadPool._dataPortionSize)
                {
                    _dataPortionSize = (int)(inFile.Length - inFile.Position);
                }
                else
                {
                    _dataPortionSize = ThreadPool._dataPortionSize;
                }

                byte[] dataPortion = new byte[_dataPortionSize];
                inFile.Read(dataPortion, 0, _dataPortionSize);


                ThreadPool.QueueUserWorkItem(dataPortion);
            }
        }

        public static void WriteBlock()
        {
            byte[] dataToWrite = dataWaitingToWrite.Dequeue();
            outFile.Write(dataToWrite, 0, dataToWrite.Length);
        }
    }
    
}
