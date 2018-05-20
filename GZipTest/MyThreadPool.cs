using System;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.IO.Compression;

namespace GZipTest
{
    public class MyThreadPool
    {
        delegate void Compression();
        delegate void Decompression();
        delegate void WritingToFile();
        static event Compression OnNewDataToCompress;
        static event Decompression OnNewDataToDecompress;
        static FileStream outFile;
        static Thread outputThread;
        static SortedList<int, KeyValuePair<int, byte[]>> dataWaitingToWrite;
        static Queue<KeyValuePair<int, byte[]>> CompressionQueue;
        static Queue<KeyValuePair<int, KeyValuePair<int, byte[]>>> DecompressionQueue;
 //       static ThreadSafeQueue<KeyValuePair<int, byte[]>> CompressionQueue;
//        static ThreadSafeQueue<KeyValuePair<int, KeyValuePair<int, byte[]>>> DecompressionQueue;
        static int dataPortionToCompressIndex;
        static object lockObjectOne;
        static object lockObjectTwo;
        static Semaphore sem = new Semaphore(1, 1);
        static int maxThreadsNumbers = 1024;    //потому что размер стека одного потока равен 1 мб
        public static int _dataPortionSize = 1042 * 1024;                           //(int)(Environment.WorkingSet / maxThreadsNumbers);//SystemPageSize вовращает размер доступной памяти для данного процесса
        static int currentWorkingThreadsNumber;

        public MyThreadPool(string fileName, string archiveName, string mode)
        {
            dataWaitingToWrite = new SortedList<int, KeyValuePair<int, byte[]>>();
            CompressionQueue = new Queue<KeyValuePair<int, byte[]>>();
            DecompressionQueue = new Queue<KeyValuePair<int, KeyValuePair<int, byte[]>>>();
            dataPortionToCompressIndex = 0;
            lockObjectOne = new object();
            lockObjectTwo = new object();
            OnNewDataToCompress += InitCompression;
            OnNewDataToDecompress += InitDecompression;
            if (mode == "Compress")
            {
                outFile = new FileStream(@"D:\Downloads\Test\" + archiveName, FileMode.Append);
            }
            else
            {
                outFile = new FileStream(@"D:\Downloads\Test\" + fileName, FileMode.Append);
            }
            outputThread = new Thread(new ThreadStart(WriteBlock));
            outputThread.Start();
            currentWorkingThreadsNumber = 1;
        }

        public static bool QueueUserWorkItem(KeyValuePair<int,byte[]>kvp)
        {

            CompressionQueue.Enqueue(new KeyValuePair<int, byte[]>(kvp.Key, kvp.Value));
            OnNewDataToCompress();
            return true;

        }
        public static bool QueueUserWorkItem(byte[] dataToDecompress, int dataPortion)
        {
            DecompressionQueue.Enqueue(new KeyValuePair<int, KeyValuePair<int, byte[]>>(dataPortionToCompressIndex++, new KeyValuePair<int, byte[]>(dataPortion, dataToDecompress)));
            OnNewDataToDecompress();
            return true;

        }

        public static void InitCompression()
        {
            Thread t = new Thread(new ThreadStart(CompressBlock));
            t.Start();
            currentWorkingThreadsNumber++;
        }

        public static void InitDecompression()
        {
            Thread t = new Thread(new ThreadStart(DecompressBlock));
            t.Start();
            currentWorkingThreadsNumber++;
        }

        public static int AvailableThreads()
        {
            return maxThreadsNumbers - currentWorkingThreadsNumber;
        }

        public static void CompressBlock()
        {
            byte[] dataPortion;
            byte[] _data;
            int dataPortionToWriteIndex;
            KeyValuePair<int, byte[]> kvp;

            if (CompressionQueue.Count != 0)
            {
                lock (CompressionQueue)
                {
                    kvp = CompressionQueue.Dequeue();
                }

                dataPortion = kvp.Value;
                dataPortionToWriteIndex = kvp.Key;
                using (MemoryStream output = new MemoryStream(dataPortion.Length))
                {
                    using (GZipStream cs = new GZipStream(output, System.IO.Compression.CompressionMode.Compress))
                    {
                        cs.Write(dataPortion, 0, dataPortion.Length);
                    }
                    _data = output.ToArray();
                }
                byte[] _lengthData = BitConverter.GetBytes(_data.Length);
                _lengthData.CopyTo(_data, 4);
                sem.WaitOne();
                dataWaitingToWrite.Add(kvp.Key, new KeyValuePair<int, byte[]>(kvp.Key, _data));
                sem.Release();
            }
            currentWorkingThreadsNumber--;
        }

        public static void DecompressBlock()
        {
            byte[] dataPortion;
            int _dataPortionSize;
            KeyValuePair<int, KeyValuePair<int, byte[]>> kvp;
            lock (lockObjectOne)
            {
                kvp = DecompressionQueue.Dequeue();
                dataPortion = kvp.Value.Value;
                _dataPortionSize = kvp.Value.Key;
            }
            byte[] _data = new byte[_dataPortionSize];
            using (MemoryStream output = new MemoryStream(dataPortion))
            {
                using (GZipStream ds = new GZipStream(output, System.IO.Compression.CompressionMode.Decompress))
                {
                    ds.Read(_data, 0, _data.Length);
                }
            }
            sem.WaitOne();
                dataWaitingToWrite.Add(kvp.Key, new KeyValuePair<int, byte[]>(kvp.Key, _data));
            sem.Release();
            currentWorkingThreadsNumber--;
        }

        public static void WriteBlock()
        {
            int index = 0;
            int count = 0;
            while (count != 20)
            {
                while ((dataWaitingToWrite.Count == 0) && (count != 20))
                {
                    Thread.Sleep(100);
                    count++;
                }
                if (count != 20)
                {
                    if (dataWaitingToWrite.ContainsKey(index))
                    {
                        MyThreadPool.outFile.Write(dataWaitingToWrite[index].Value, 0, dataWaitingToWrite[index].Value.Length);
                        dataWaitingToWrite.RemoveAt(0);
                        index++;
                    }
                    count = 0;
                }
            }
            MyThreadPool.outFile.Close();
        }

    }
}
