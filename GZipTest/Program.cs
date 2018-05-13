using System;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.IO.Compression;


namespace GZipTest
{
    static public class GZipTest
    {
        static string fileName;
        static int dataPortionToCompressIndex = 0;
        static int prevDataPortionToWriteIndex = -1;
        static Object lockObject;

        static void Main(string[] args)
        {
            lockObject = new Object();
            fileName = @"D:\Downloads\Test\Test.mkv";
            try
            {
            MyThreadPool pool = new MyThreadPool("compress");
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
        public static event Decompression OnNewDataToDecompress;
        static SortedList<int, byte[]> dataWaitingToCompress;
        static SortedList<int, KeyValuePair<int, byte[]>> dataWaitingToWrite;
        static Queue<KeyValuePair<int, byte[]>> CompressionQueue;
        static Queue<KeyValuePair<int, KeyValuePair<int, byte[]>>> DecompressionQueue;

        public class MyThreadPool
        {
            public static FileStream outFile;
            static Queue<Thread> currentWorkingTreads;
            public static Thread outputThread;
            static string CompressionMode;

            static int maxThreadsNumbers = (int)(Environment.WorkingSet / 1024*1024);    //потому что размер стека одного потока равен 1 мб
            public static int _dataPortionSize = 1042 * 1024;                           //(int)(Environment.WorkingSet / maxThreadsNumbers);//SystemPageSize вовращает размер доступной памяти для данного процесса

            public MyThreadPool(string mode)
            {
                OnNewDataToCompress += InitCompression;
                OnNewDataToDecompress += InitDecompression;
                CompressionMode = mode;
                dataWaitingToCompress = new SortedList<int, byte[]>();
                dataWaitingToWrite = new SortedList<int, KeyValuePair<int, byte[]>>();
                currentWorkingTreads = new Queue<Thread>();
                CompressionQueue = new Queue<KeyValuePair<int, byte[]>>();
                DecompressionQueue = new Queue<KeyValuePair<int, KeyValuePair<int, byte[]>>>();
                if (mode == "compress")
                {
                    outFile = new FileStream(Path.GetFullPath(fileName) + ".gz", FileMode.Append);
                }
                else
                {
                    outFile = new FileStream(Path.GetFullPath(fileName.Remove(fileName.Length - 3)), FileMode.Append);
                }
                outputThread = new Thread(new ThreadStart(WriteBlock));
                outputThread.Start();
            }

            public static bool QueueUserWorkItem(byte[] dataToCompress)
            {
                
                CompressionQueue.Enqueue(new KeyValuePair<int, byte[]>(dataPortionToCompressIndex++, dataToCompress));
                OnNewDataToCompress();
                return true;
                
            }
            public static bool QueueUserWorkItem(byte[] dataToDecompress, int dataPortion)
            {
                DecompressionQueue.Enqueue(new KeyValuePair<int, KeyValuePair<int, byte[]>>(dataPortionToCompressIndex++, new KeyValuePair < int, byte[] > (dataPortion, dataToDecompress)));
                OnNewDataToDecompress();
                return true;
            
            }

            public static void InitCompression()
            {
                Thread t = new Thread(new ThreadStart(CompressBlock));
                t.Start();
                currentWorkingTreads.Enqueue(t);
            }

            public static void InitDecompression()
            {
                Thread t = new Thread(new ThreadStart(DecompressBlock));
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
            byte[] _data;
            int dataPortionToWriteIndex;

            lock(lockObject)
            {
                KeyValuePair<int, byte[]> kvp = CompressionQueue.Dequeue();
                dataPortion = kvp.Value;
                dataPortionToWriteIndex = kvp.Key;
            }
            using (MemoryStream output = new MemoryStream(dataPortion.Length))
            {
                using (GZipStream cs = new GZipStream(output, CompressionMode.Compress))
                {
                    cs.Write(dataPortion, 0, dataPortion.Length);
                }
                _data = output.ToArray();
            }
            byte[] _lengthData = BitConverter.GetBytes(_data.Length);
            _lengthData.CopyTo(_data, 4);
            lock(lockObject)
            {
                dataWaitingToWrite.Add(dataPortionToWriteIndex, new KeyValuePair<int,byte[]>(dataPortionToWriteIndex,_data));
            }
        }

        public static void Compress(string inFileName)
        {
            FileStream inFile = new FileStream(Path.GetFullPath(inFileName), FileMode.Open);
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

        public static void Decompress(string inFileName)
        {
            FileStream inFile = new FileStream(inFileName, FileMode.Open);
            byte[] buffer = new byte[8];
            int compressedBlockLength;
            int _dataPortionSize;
            while (inFile.Position != inFile.Length)
            {
                inFile.Read(buffer, 0, 8);
                compressedBlockLength = BitConverter.ToInt32(buffer, 4);
                byte[] compressedDataArray = new byte[compressedBlockLength + 1];
                buffer.CopyTo(compressedDataArray, 0);

                inFile.Read(compressedDataArray, 8, compressedBlockLength - 8);
                _dataPortionSize = BitConverter.ToInt32(compressedDataArray, compressedBlockLength - 4);
                while (dataWaitingToWrite.Count + dataWaitingToCompress.Count >= 600)
                {
                    Thread.Sleep(300);
                }
                MyThreadPool.QueueUserWorkItem(compressedDataArray, _dataPortionSize);
            }

        }

        public static void DecompressBlock()
        {
            byte[] dataPortion;
            int dataPortionToWriteIndex;
            int _dataPortionSize;
            lock (lockObject)
            {
                KeyValuePair<int, KeyValuePair<int, byte[]>> kvp = DecompressionQueue.Dequeue();
                dataPortion = kvp.Value.Value;
                _dataPortionSize = kvp.Value.Key;
                dataPortionToWriteIndex = kvp.Key;
            }
            byte[] _data = new byte[_dataPortionSize];
            using (MemoryStream output = new MemoryStream(dataPortion))
            {
                using (GZipStream ds = new GZipStream(output, CompressionMode.Decompress))
                {
                    ds.Read(_data, 0, _data.Length);
                }
            }
            dataWaitingToWrite.Add(dataPortionToWriteIndex, new KeyValuePair<int, byte[]>(dataPortionToWriteIndex, _data));
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
                    dataToWrite = dataWaitingToWrite[prevDataPortionToWriteIndex + 1].Value;
                    prevDataPortionToWriteIndex++;
                    dataWaitingToWrite.Remove(prevDataPortionToWriteIndex);
                    MyThreadPool.outFile.Write(dataToWrite, 0, dataToWrite.Length);
                    count = 0;
                }
            }
            MyThreadPool.outFile.Close();
        }
    }
    
}
