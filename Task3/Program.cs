using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Task3
{
    // Хранилище данных со строками
    public static class Storage
    {
        public static List<string> UnsortedStrings { get; set; } = new List<string>();
        public static List<string> SortedStrings { get; set; } = new List<string>();
    }

    internal class Program
    {
        static void Main(string[] args)
        {
            // Установка начальных значений
            int threadCount = 5;
            string filePath = @"C:\Users\Gleb\Downloads\TestTxtFile.txt";

            TaskQueue tasksQueue = new TaskQueue(threadCount);
            StringsTaker stringsTaker = new StringsTaker(filePath);

            // Получаем список неотсортированных строк в объект Storage
            Storage.UnsortedStrings = stringsTaker.TakeStrings(threadCount);

            Action action = SortString;

            // Помещаем задачи на сортировку в очередь на выполнение
            for (int i = 0; i < threadCount; i++)
                tasksQueue.EnqueueTask(SortString);

            // Ждём завершения задач, после чего закрываем потоки
            tasksQueue.Close();

            for (int i = 0; i < Storage.SortedStrings.Count; i++)
            {
                Console.WriteLine($"String №{i+1}: {Storage.SortedStrings[i]}\n");
            }

            StringBuilder sb = new StringBuilder(string.Empty);

            foreach (string str in Storage.SortedStrings)
                sb.AppendLine(str);


            Console.WriteLine($"{string.Concat(sb.ToString().OrderBy(c => c)).Replace(" ", "").Replace("\n", "")}");

            Console.ReadLine();

        }

        static void SortString()
        {
            string unsortedString;

            lock(Storage.UnsortedStrings)
            {
                unsortedString = Storage.UnsortedStrings.First();
                Storage.UnsortedStrings.RemoveAt(0);

                Monitor.Pulse(Storage.UnsortedStrings);
            }

            string sortedString = string.Concat(unsortedString.OrderBy(c => c));

            lock(Storage.SortedStrings)
            {
                Storage.SortedStrings.Add(sortedString);

                Monitor.Pulse(Storage.SortedStrings);
            }
        }
    }

    public class StringsTaker
    {
        private readonly string filePath;

        public StringsTaker(string filePath)
        {
            this.filePath = filePath;
        }

        public List<string> TakeStrings(int threadsCount)
        {
            List<string> result = new List<string>();

            string text = File.ReadAllText(filePath);

            int startIndex = 0;
            int length = text.Length / threadsCount;

            for (int i = 0; i < threadsCount - 1; i++)
            {
                result.Add(text.Substring(startIndex, length));
                startIndex = length * (i + 1);
            }

            if (text.Length % threadsCount == 0)
                result.Add(text.Substring(startIndex, length));
            else
                result.Add(text.Substring(startIndex, length + text.Length % threadsCount));

            return result;
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
