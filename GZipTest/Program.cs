using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using System.IO.Compression;


namespace GZipTest
{
    static public class GZipTest
    {

        static int threadNumber = Environment.ProcessorCount;

        //static Thread[] tPool = new Thread[threadNumber];

        static byte[][] dataArray = new byte[threadNumber][];
        static byte[][] compressedDataArray = new byte[threadNumber][];

        static int dataPortionSize = 10000000;
        static int dataArraySize = dataPortionSize * threadNumber;

        static void Main(string[] args)
        {

            string fileName = "D:\\original.jpg";



            Compress(fileName);
        }

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
        }
    }
}
