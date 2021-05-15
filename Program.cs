using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LAB6_Parallel
{
    class Visitor //Класс , позволяющий хранить информацию о поситителе
    {
        public string Name; //номер
        public string ArrivalTime; //время прихода
        public TimeSpan tsArrivalTime;
        public string WaitTimeInQ; //время ожидания
        public Stopwatch WaitingTime;
        public string status; // Wait - ждет в очреди, InWork - обрабатывается, Finished - обработан
        public string numberOfWindow; //номер окна обслуживания
        public string serviceTime; //время обслуживания
        public string score;   //оценка работы
        public string ExitTime;//время выхода
        public Visitor(string _name)
        {
            Name = _name;
            WaitingTime = new Stopwatch();
            WaitingTime.Start();
            status = "Wait";
            ArrivalTime = "";
            WaitTimeInQ = "";
            numberOfWindow = "";
            serviceTime = "";
            score = "";
        }
    }
    class WorkingWindow
    {
        public string Name;
        public int NumberOfServiced;
        public double averageWorkGrade;
        public WorkingWindow(string _name)
        {
            Name = _name;
            NumberOfServiced = 0;
            averageWorkGrade = 0;
        }
    }
    class Program
    {
        static object locker = new object();
        static public Random rnd; //В программе должен быть только один объект типа рандом
        static int N = 4; //Начальное кол-во окон
        static Stopwatch mainStopWatch; //Главное время (секундомер)
        static bool WorkingDay; //Рабочий день
        static public int GetRandomTimeInMinutes()//Случайное время на время работы с клиентом
        {
            lock (locker)//Блокируем, потому что были случаи когда одно и тоже время брали
            {
                rnd = new Random();
                return rnd.Next(5, 21);
            }
        }
        static public int GetRandomScore()//Оценка от пользователя
        {
            lock (locker) //Блокируем потому, что могут взять одну и ту же оценку
            {
                rnd = new Random();
                return rnd.Next(1, 6);
            }
        }
        //дельта t = –Ln(E)/лямбда. 
        static public int PoissonFlow() //Время на приход нового клиента
        {
            rnd = new Random();
            double E = rnd.NextDouble();
            double ly = 0.1; //0.1 Наиболее оптимальная скорость
            return (int)(-Math.Log(E, Math.E) / ly);
        }
        static public void GoWork()
        {
            Visitor myVisitor = new Visitor("Temp");
            bool ImBusy = false; //Факт того, занят ли я? Если нет беру клиента
            bool WorkHard = false; //Работаем с клиентом
            Stopwatch myStopWatch;
            Stopwatch chillTime;
            TimeSpan ts;
            int needWorkTime = 0;
            WorkingDay = true;
            chillTime = new Stopwatch();
            chillTime.Start();
            while (WorkingDay)
            {
                //Если я простаиваю больше 20 минут(секунд в нешем понимании), закрываюсь...
                ts = chillTime.Elapsed;
                if (ts.TotalSeconds >= 20)
                {
                    ts = mainStopWatch.Elapsed;
                    Console.WriteLine(GetGoodTime(ts.TotalMilliseconds) + " Окно " + Thread.CurrentThread.Name + " закрылось из-за бездействия.");
                    Thread.CurrentThread.Abort();//В условии нет ничего о том, должно ли это окно в последующем заного начать работать, поэтому просто удаляем его..
                }
                lock (locker)//Блокируем, т.к. клиента будет брать только один поток!
                {
                    if (ImBusy == false && People.Count != 0)//Если свободен, заступаем на обслуживание
                    {
                        chillTime.Restart();
                        ImBusy = true;
                        //Берем из очереди клиента
                        myVisitor = People.Dequeue();
                        foreach (WorkingWindow i in StatWindows)
                        {
                            if (i.Name == Thread.CurrentThread.Name)
                            {
                                i.NumberOfServiced++;
                            }
                        }
                        ts = mainStopWatch.Elapsed;
                        myVisitor.WaitTimeInQ = GetGoodTime(ts.TotalMilliseconds - myVisitor.tsArrivalTime.TotalMilliseconds);
                        myVisitor.numberOfWindow = Thread.CurrentThread.Name;
                        myVisitor.status = "InWork";
                        Console.WriteLine(GetGoodTime(ts.TotalMilliseconds) + " Клиент " + myVisitor.Name + " подошёл к окну " + Thread.CurrentThread.Name + ".");
                        WorkHard = true;
                    }
                }
                //Работаем с клиентом
                //Окно обслуживает клиента случайное время в интервале от 5 до 20 минут
                myStopWatch = new Stopwatch();
                myStopWatch.Start();
                ts = mainStopWatch.Elapsed;
                int TimeToWorkWithClient = GetRandomTimeInMinutes();
                needWorkTime = (int)(ts.TotalSeconds) + TimeToWorkWithClient;
                while (WorkHard)
                {
                    chillTime.Restart();
                    Thread.Sleep(10);//Уменьшаем нагрузку 
                    ts = mainStopWatch.Elapsed;
                    if (ts.TotalSeconds >= needWorkTime) // Если время работы прошло, заканчиваем
                    {
                        string _score = GetRandomScore().ToString();
                        Console.WriteLine(GetGoodTime(ts.TotalMilliseconds) + " Клиент " + myVisitor.Name + " закончил обслуживание с оценкой " + _score + ".");
                        myVisitor.serviceTime = GetGoodTime(TimeToWorkWithClient * 1000);
                        myVisitor.score = _score;
                        foreach (WorkingWindow i in StatWindows)
                        {
                            if (i.Name == Thread.CurrentThread.Name)
                            {
                                i.averageWorkGrade += Convert.ToInt32(_score);
                            }
                        }
                        myVisitor.ExitTime = GetGoodTime(ts.TotalMilliseconds);
                        myVisitor.status = "Finished";
                        WorkHard = false;
                        ImBusy = false;
                    }
                }
            }
        }
        static Queue<Visitor> People;
        static List<Visitor> PeopleStatForCsvFile;
        static List<WorkingWindow> StatWindows;
        static void Main(string[] args)
        {
            mainStopWatch = new Stopwatch();
            TimeSpan ts;
            People = new Queue<Visitor>();//Очередь людей
            PeopleStatForCsvFile = new List<Visitor>(); //Дублируем, в конце возьмём от сюда всю информацию
            StatWindows = new List<WorkingWindow>();
            int counterOfWindow = 1;
            //Создание потоков
            List<Thread> WorkWindows = new List<Thread> { };//рабочие Окна
            for (int i = 0; i < N; i++)
            {
                Thread tempThread = new Thread(GoWork);
                tempThread.Name = counterOfWindow.ToString();
                StatWindows.Add(new WorkingWindow(counterOfWindow.ToString()));
                counterOfWindow++;
                WorkWindows.Add(tempThread);
            }
            //Запускаем работу
            for (int i = 0; i < N; i++)
            {
                WorkWindows[i].Start();
            }
            int countPeople = 1;
            bool work = true; //Работа именно центра , отвечающего за все
            bool add = true; //Добовляем еще одного клиента если прошло п. время
            bool WeCanTake = true; //Принимаем клиентов или нет?
            bool Lever = true; //Затычка
            int SavedTimeInSeconds = PoissonFlow();
            Console.WriteLine("00:00:00 Старт рабочего дня...");
            mainStopWatch.Start();
            while (work)
            {
                Thread.Sleep(100);//Уменьшаем нагрузку, + даем время Окнам обновить информацию
                //Если 3 человека долго ждет, добовляем 2 потока (2 новых окна)
                string WhoLongWaits = "";
                Visitor[] CopyVisitors = People.ToArray();
                int countToCheck = 0; //Если будет равен 3 то открываем 2 новых окна
                for (int i = 0; i < CopyVisitors.Length; i++)
                {
                    ts = CopyVisitors[i].WaitingTime.Elapsed;
                    if (CopyVisitors[i].status == "Wait" && ts.TotalSeconds >= 40)
                    {
                        WhoLongWaits += CopyVisitors[i].Name + " ";
                        countToCheck++;
                    }
                }
                if (countToCheck >= 3)
                {
                    Thread OneThread = new Thread(GoWork);
                    OneThread.Name = counterOfWindow.ToString();
                    StatWindows.Add(new WorkingWindow(counterOfWindow.ToString()));
                    counterOfWindow++;
                    WorkWindows.Add(OneThread);
                    Thread TwoThread = new Thread(GoWork);
                    TwoThread.Name = counterOfWindow.ToString();
                    StatWindows.Add(new WorkingWindow(counterOfWindow.ToString()));
                    counterOfWindow++;
                    WorkWindows.Add(TwoThread);
                    ts = mainStopWatch.Elapsed;
                    Console.WriteLine(GetGoodTime(ts.TotalMilliseconds) + " Открыты окна " + OneThread.Name + " " + TwoThread.Name + " т.к. клиенты " + WhoLongWaits + " ждут больше 40 минут.");
                    OneThread.Start();
                    TwoThread.Start();
                }
                if (add == true && WeCanTake)
                {
                    ts = mainStopWatch.Elapsed;
                    Console.WriteLine(GetGoodTime(ts.TotalMilliseconds) + " Клиент " + countPeople + " взял талон.");
                    Visitor TempVisitor = new Visitor(countPeople.ToString());
                    TempVisitor.ArrivalTime = GetGoodTime(ts.TotalMilliseconds);
                    TempVisitor.tsArrivalTime = ts;
                    People.Enqueue(TempVisitor);
                    PeopleStatForCsvFile.Add(TempVisitor);
                    countPeople++;
                    add = false;
                }
                ts = mainStopWatch.Elapsed;
                //Проверка на добавление посетителя
                if (ts.TotalSeconds >= SavedTimeInSeconds)
                {
                    SavedTimeInSeconds += PoissonFlow();
                    add = true;
                }
                //Закрываемся за 21 минуту до якобы конца рабочего дня. Т.к. клиент может обслуживаться ровно 20 мин
                //Увы но если кто то еще будет в очереди, то он не успеет. Все как в реальной жизни!
                if (ts.TotalSeconds >= 699 && Lever)//699
                {
                    ts = mainStopWatch.Elapsed;
                    Console.WriteLine(GetGoodTime(ts.TotalMilliseconds) + " Больше не пускаем посетителей!");
                    WeCanTake = false;
                    Lever = false;

                }
                if (ts.TotalSeconds >= 720)//720   12часов
                {
                    work = false;
                    WorkingDay = false;
                    Thread.Sleep(1000); //Ждем 1 секунду, пока потоки выведут инфо по окончанию дня
                    for (int i = 0; i < WorkWindows.Count; i++)
                    {
                        WorkWindows[i].Abort();
                        WorkWindows[i].Join();
                    }
                }
            }
            ts = mainStopWatch.Elapsed;
            Console.WriteLine(GetGoodTime(ts.TotalMilliseconds) + " Конец рабочего дня...");
            //Выводим информацию об окнах
            foreach (WorkingWindow i in StatWindows)
            {
                //Если мы обслужили хотя бы одного
                if (i.NumberOfServiced > 0)
                    Console.WriteLine("*Окно: " + i.Name + " обслужило " + i.NumberOfServiced + " посетителя/лей. Средняя оценка: " + i.averageWorkGrade / i.NumberOfServiced);
                else
                {
                    Console.WriteLine("*Окно: " + i.Name + " никого не обслуживало");
                }
            }
            var output = new StringBuilder();
            //Записываем файл csv
            output.AppendLine("Номер\tВремя прихода\tВремя ожидания\tНомер окна\tВремя обслуживания\tОценка работы\tВремя выхода");
            for (int i = 0; i < PeopleStatForCsvFile.Count; i++)
            {
                output.AppendLine(PeopleStatForCsvFile[i].Name + "\t" + PeopleStatForCsvFile[i].ArrivalTime + "\t" +
                     PeopleStatForCsvFile[i].WaitTimeInQ + "\t" + PeopleStatForCsvFile[i].numberOfWindow + "\t" +
                     PeopleStatForCsvFile[i].serviceTime + "\t" + PeopleStatForCsvFile[i].score + "\t" +
                     PeopleStatForCsvFile[i].ExitTime);
            }
            File.WriteAllText(@"../../Clients.csv", output.ToString(), Encoding.Unicode);
            Console.ReadLine();
        }
        static public string GetGoodTime(double AllTimeInMilliSeconds) //Время в красивом формате
        {
            double ConvertedTime = AllTimeInMilliSeconds / (1000 / 60); //Будут погрешности, но это никак не мешает. Симуляция работает правильно.
            string hour = "0";
            string min = "0";
            string sec = "0";
            if (ConvertedTime >= 3600)
            {
                int _h = (int)ConvertedTime / 3600;//Hour
                hour = _h.ToString();

                ConvertedTime -= (3600 * _h);
            }
            if (ConvertedTime >= 60)
            {
                int _m = (int)ConvertedTime / 60;//Min
                min = _m.ToString();

                ConvertedTime -= (_m * 60);
            }
            int _s = (int)ConvertedTime;//Sec
            sec = _s.ToString();
            ConvertedTime -= (_s * 60);
            if (hour.Length == 1)
            {
                hour = "0" + hour;
            }
            if (min.Length == 1)
            {
                min = "0" + min;
            }
            if (sec.Length == 1)
            {
                sec = "0" + sec;
            }
            return hour + ":" + min + ":" + sec;
        }
    }
}