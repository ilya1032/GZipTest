using System;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.IO.Compression;


namespace GZipTest
{
    public static class GZipArchiver
    {

        static void Main(string[] args)
        {
            string mode = null;
            string fileName = null;
            string archiveName = null;

            /*
            try
            {
                mode = "Compress";
                fileName = "Test.mkv";
                archiveName = "Test.mkv.gz";
            }
            */
            try
            {
                mode = args[0];
                if (mode != "Compress" && mode != "Decompress")
                    throw new ArgumentException();
                if (args[1].EndsWith(".gz"))
                {
                    archiveName = args[1];
                    fileName = args[2];
                }
                else
                {
                    if (args[2].EndsWith(".gz"))
                    {
                        archiveName = args[2];
                        fileName = args[1];
                    }
                    else
                    {
                        throw new ArgumentException();
                    }
                }
            }

            catch (ArgumentException ex)
            {
                Console.WriteLine(ex.Message);
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return;
            }

            if (mode == "Compress")
            {
                MyThreadPool pool = new MyThreadPool(fileName, archiveName, mode);
                Compress(fileName);
            }
            else
            {
                MyThreadPool pool = new MyThreadPool(fileName, archiveName, mode);
                Decompress(archiveName);
            }

            return;
        }

        public static void Compress(string inFileName)
        {
            FileStream inFile = new FileStream(@"D:\Downloads\Test\" + inFileName, FileMode.Open);
            int _dataPortionSize;
            int portionIndex = 0;
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

                while (MyThreadPool.AvailableThreads() < 1)
                {
                    Thread.Sleep(300);
                }
                MyThreadPool.QueueUserWorkItem(new KeyValuePair<int,byte[]>(portionIndex,dataPortion));
                portionIndex++;    
            }

            inFile.Close();
        }

        public static void Decompress(string inFileName)
        {
            FileStream inFile = new FileStream(@"D:\Downloads\Test\" + inFileName, FileMode.Open);
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
                while (MyThreadPool.AvailableThreads() < 1)
                {
                    Thread.Sleep(300);
                }
                MyThreadPool.QueueUserWorkItem(compressedDataArray, _dataPortionSize);
            }
            inFile.Close();
        }
    }
    
}
