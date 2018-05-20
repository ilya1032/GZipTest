using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace GZipTest
{
    public class ThreadSafeQueue<T> 
    {
        private Queue<T> _queue;
        Semaphore dequeLock;
        Semaphore enqueLock;

        public ThreadSafeQueue()
        {
            _queue = new Queue<T>();
            dequeLock = new Semaphore(1,1);
            enqueLock = new Semaphore(1, 1);
        }

        public void Enqueue(T _data)
        {
            enqueLock.WaitOne();
            _queue.Enqueue(_data);
            enqueLock.Release();
            return;
        }

        public T Dequeue()
        {
            T temp;
            dequeLock.WaitOne();
            temp = _queue.Dequeue();
            dequeLock.Release();
            return temp;
        }

        public bool isEmpty()
        {
            return (_queue.Count == 0);
        }
    }
}
