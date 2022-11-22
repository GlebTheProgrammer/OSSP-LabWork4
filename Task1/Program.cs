using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace Task1
{
    public class ExAction
    {
        // Action - заглушка
        public static void PrintHello()
        {
            Console.WriteLine("Hello. Im a Task");
        }
    }

    internal class Program
    {
        static void Main(string[] args)
        {
            TaskQueue tasksQueue = new TaskQueue();
            Action action = ExAction.PrintHello;

            // Будет запущено 10 потоков
            for (int i = 0; i < 10; i++)
            {
                // В каждом запускается задача на помещение в очередь action на выполнение
                new Thread(() =>
                {
                    tasksQueue.EnqueueTask(action);
                }).Start();
            }

            
            Console.WriteLine();
        }
    }

    public class TaskQueue
    {
        // Очередь задач на выполнение
        private Queue<Action> tasks;

        public TaskQueue()
        {
            // Создаём пустой список задач
            tasks = new Queue<Action>();
        }

        // Метод для помещения задачи в очередь на выполнение
        public void EnqueueTask(Action task)
        {
            // Забираем блокировку на объект очереди
            lock (tasks)
            {
                // Помещаем в очередь задачу на выполнение
                tasks.Enqueue(task);
                Console.WriteLine("We have a new task to do!");
                Thread.Sleep(1000);
            }
        }
    }
}
