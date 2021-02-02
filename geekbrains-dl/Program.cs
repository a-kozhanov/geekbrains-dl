using System;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace GeekBrainsDownloader
{
    class Program
    {
        //private const string Dir = "./Courses/";
        private const string ConsoleTitle = "geekbrains-dl v0.1 beta";

        static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.Title = ConsoleTitle;

            //email password idCourse
            if (args.Length != 3)
            {
                Console.WriteLine("Неверное количество параметров");
                Console.WriteLine("Пример использования: geekbrains-dl.exe email password idCourseFromUrl");
                //Console.ReadKey();
                return;
            }

            if (string.IsNullOrWhiteSpace(args[0]))
            {
                Console.WriteLine("Неверно указан email");
                //Console.ReadKey();
                return;
            }

            try
            {
                MailAddress m = new MailAddress(args[0]);
            }
            catch (FormatException)
            {
                Console.WriteLine("Неверной формат email");
                //Console.ReadKey();
                return;
            }

            if (string.IsNullOrWhiteSpace(args[1]))
            {
                Console.WriteLine("Неверно указан пароль");
                //Console.ReadKey();
                return;
            }

            if (string.IsNullOrWhiteSpace(args[2]))
            {
                Console.WriteLine("Неверно указан idCourse");
                //Console.ReadKey();
                return;
            }

            GbDownloader gbDownloader = new GbDownloader(args[0].Trim(), args[1].Trim(), args[2].Trim());
            await gbDownloader.Login();

            if (!gbDownloader.Auth)
            {
                Console.WriteLine("Неверный email или пароль");
                //Console.ReadKey();
                return;
            }

            await gbDownloader.OpenCourse();

            if (gbDownloader.CourseMentors.Count == 0)
            {
                Console.WriteLine("Не удалось найти преподавательский состав");
                //Console.ReadKey();
                return;
            }

            if (string.IsNullOrWhiteSpace(gbDownloader.CourseTitle))
            {
                Console.WriteLine("Не удалось найти название курса");
                //Console.ReadKey();
                return;
            }

            await gbDownloader.GatherDataCourse();

            if (gbDownloader.LessonsData.Count == 0)
            {
                Console.WriteLine("Не удалось найти файлы для скачивания");
                //Console.ReadKey();
                return;
            }

            await gbDownloader.GetDataCourse();

            await gbDownloader.Logout();

            Console.WriteLine("Cкачивание курса завершено");
            //Console.ReadKey();
        }
    }
}