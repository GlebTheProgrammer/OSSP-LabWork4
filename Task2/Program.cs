using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Task2
{
    public class ExThread
    {
        // Action для тестирования
        public static void PrintPID()
        {
            for (int i = 0; i < 10; i++)
            {
                Console.WriteLine($"Message from Thread with PID: {Thread.CurrentThread.ManagedThreadId}");
                Thread.Sleep(1000);
            }
        }
    }

    internal class Program
    {

        static void Main(string[] args)
        {
            // Создаём очередь, а также объект делегата с ссылкой на метод
            TaskQueue tasksQueue = new TaskQueue(2);
            Action action = ExThread.PrintPID;

            // Будет запущено 2 потока
            for (int i = 0; i < 2; i++)
            {
                // Каждый поток вставляет в очередь заданий action на обработку атомарно
                Thread.Sleep(1000);
                new Thread(() =>
                {
                    Console.WriteLine($"Task {i} from Thread with PID : {Thread.CurrentThread.ManagedThreadId} has been pulled into the queue");
                    tasksQueue.EnqueueTask(action);
                }).Start();
            }

            // Перед выходом из программы - ждём завершение всех активных и стоящих в очереди задач, после чего приостанавливаем выполнение потоков
            tasksQueue.Close();
        }
    }

    public class TaskQueue
    {
        // Список активных потоков
        private List<Thread> threads;

        // Очередб задач на выполнение
        private Queue<Action> tasks;

        // Конструктор для активации объекта
        public TaskQueue(int threadCount)
        {
            // Создаём пустые списки
            tasks = new Queue<Action>();
            threads = new List<Thread>();

            // Метод для запуска указанного в параметрах числа активных потоков
            for (int i = 0; i < threadCount; i++)
            {
                // Устанавливаем потоку Default-ную задачу на выполнение, добавляем наш поток в список, устанавливаем его как фоновый и активируем его (начинаем выполнять Default-ную задачу)
                var t = new Thread(DoThreadWork);
                threads.Add(t);
                t.IsBackground = true;
                t.Start();
            }
        }

        // Default-ный action для помещения в качестве метода на выполнение в только что созданный поток
        private void DoThreadWork()
        {
            // Имитация работы потока (работает всё время)
            while (true)
            {
                // Пытаемся извлечь в качестве задачи на выполнение action из очереди
                Action task = DequeueTask();

                // Если задача на выполнение не null 
                if (task != null)
                {
                    // Пытаемся выполнить action
                    try
                    {
                        task();
                    }
                    catch (ThreadAbortException)
                    {
                        // Если каким-то образом поток закрывается внутри метода -> отменяем закрытие
                        Thread.ResetAbort();
                    }
                    catch (Exception ex)
                    {
                        // Если во время выполнения возникла ошибка -> выводим её в консоль
                        Console.WriteLine(ex);
                    }
                }
                else // Если задача на выполнение null (сигнал от нас о закрытии потоков) -> Выход из цикла. Поток прекращает работу, тк задача DoThreadWork() была выполнена
                    break;
            }
        }

        // Метод для извлечения Action из очереди
        private Action DequeueTask()
        {
            // Забираем блокировку на объект очереди, чтоб можно было работать с ним атомарно
            lock (tasks)
            {
                // Пока число задач на выполнение в очереди = 0 -> отдаём объект блокировки и ставим метод в очередь на ожидание
                while (tasks.Count == 0)
                    Monitor.Wait(tasks);

                // Как только очередь задач не пуста и данный метод (поток) захватил себе блокировку -> возвращаем задачу на выполнение данному потоку и освобожаем блокировку
                return tasks.Dequeue();
            }
        }

        // Метод для помещения задачи в очередь на выполнение
        public void EnqueueTask(Action task)
        {
            // Забираем блокировку на объект очереди
            lock (tasks)
            {
                // Помещаем в очередь задачу на выполнение
                tasks.Enqueue(task);

                // Информируем стоящие в очереди потоки о поступлении нового метода в очередь на выполнение
                Monitor.Pulse(tasks);
            }
        }
        
        // Метод для закрытия всех актинвых потоков 
        public void Close()
        {
            // Посылаем каждому потоку в списке сигнал для его закрытия
            for (int i = 0; i < threads.Count; i++)
                EnqueueTask(null); 

            // Ожидаем закрытия всех потоков, после чего управление передаётся обратно, головному потоку
            foreach (Thread t in threads)
                t.Join();
        }
    }
}
