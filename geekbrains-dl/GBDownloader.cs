using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Io;

namespace GeekBrainsDownloader
{
    public class GbDownloader
    {
        private readonly string _login;
        private readonly string _password;
        private readonly string _courseId;
        private readonly DefaultHttpRequester _requester;
        private readonly IConfiguration _config;
        private const string DownloadDir = "./Courses/";
        private const string GeekBrainsUrl = "https://geekbrains.ru/";
        private const string GeekBrainsUrlEducation = "https://geekbrains.ru/education";
        private const string LessonMaterials = "Материалы.txt";
        private static readonly string[] IgnoreDownloadExt = {".html", ".htm"};
        private IBrowsingContext _context;
        public bool Auth;


        public List<string> CourseMentors;
        public string CourseTitle;
        public string CourseStart;
        public List<string> LessonsUrl = new List<string>();
        public List<Lesson> LessonsData = new List<Lesson>();


        public class LessonContent
        {
            public string Url { get; set; }
            public string Title { get; set; }
        }

        public class Lesson
        {
            public string Url { get; set; }
            public string Title { get; set; }
            public List<LessonContent> LessonContents { get; set; }
        }


        public GbDownloader(string login, string password, string courseId)
        {
            _login = login;
            _password = password;
            _courseId = courseId;

            _requester = CreateRequester();
            _config = CreateConfig();
            _context = CreateContext();
        }

        public async Task Login()
        {
            IDocument document = null;
            try
            {
                document = await _context.OpenAsync($"{GeekBrainsUrl}login");

                document.QuerySelector<IHtmlInputElement>("input#user_email").Value = _login;
                document.QuerySelector<IHtmlInputElement>("input#user_password").Value = _password;

                document = await document.QuerySelector<IHtmlInputElement>("input[type='submit']").SubmitAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }


            if (document.BaseUri.ToLower().Equals(GeekBrainsUrl) && document.Cookie.ToLower().Contains("jwt_token") &&
                document.Cookie.ToLower().Contains("registered=1"))
            {
                Auth = true;
            }
        }

        public async Task Logout()
        {
            IDocument document = await _context.OpenAsync($"{GeekBrainsUrl}logout");
            Auth = false;
        }

        public async Task OpenCourse()
        {
            if (!Auth) return;

            //https://geekbrains.ru/chapters/* - бесплатные уроки
            //https://geekbrains.ru/lessons/* - платные уроки
            IDocument document = await _context.OpenAsync($"{GeekBrainsUrl}lessons/{_courseId}");

            // //Проверка если редирект на страницу https://geekbrains.ru/education, возможно курс бесплатный тогда другой Url
            // if (document.BaseUri.ToLower() == GeekBrainsUrlEducation.ToLower())
            // {
            //     document = await _context.OpenAsync($"{GeekBrainsUrl}chapters/{_courseId}");
            // }

            //Преподавательский состав, пример: "Geek Brains"
            CourseMentors = document.QuerySelectorAll<IHtmlAnchorElement>("a.mentor-list__name")
                .Select(m => m.Text.Trim())
                .Distinct().ToList();

            //Название курса, пример: "Git. Быстрый старт"
            CourseTitle = document.QuerySelectorAll<IHtmlSpanElement>("span.course-title")
                .Select(m => m.InnerHtml.Trim())
                .FirstOrDefault();

            //Начало курса, пример: "22.06.2020 MSK (UTC+3)"
            CourseStart = document.QuerySelectorAll<IHtmlSpanElement>("span.course-title-start")
                .Select(m => m.InnerHtml.Trim()).FirstOrDefault();

            //Url урока, пример: "/chapters/942"
            LessonsUrl = document.QuerySelectorAll<IHtmlAnchorElement>("a.lesson-header").Select(m => m.Href.Trim())
                .ToList();
        }

        public async Task GatherDataCourse()
        {
            if (!Auth) return;

            for (var i = 0; i < LessonsUrl.Count; i++)
            {
                string url = LessonsUrl[i];
                Lesson lesson = new Lesson();

                if (string.IsNullOrWhiteSpace(url))
                {
                    string firstLessonUrl = LessonsUrl[0];
                    string firstLessonId = Regex.Match(firstLessonUrl, "\\d+$").Value;

                    if (!string.IsNullOrWhiteSpace(firstLessonId))
                    {
                        int id = int.Parse(firstLessonId) + i;
                        if (id == 0) break;

                        url = $"{GeekBrainsUrl}lessons/{id}";
                    }
                    else
                    {
                        break;
                    }
                }

                IDocument document = await _context.OpenAsync(url);

                lesson.Url = url;
                // lesson.Title = document.QuerySelectorAll<IHtmlSpanElement>("span.course-title").Select(m => m.InnerHtml)
                //     .FirstOrDefault();

                lesson.Title = document.QuerySelector("h3.title").Text().TrimEnd('.').Trim();

                LessonContent lessonContent = new LessonContent();

                var lessonContentUrl = document.QuerySelectorAll<IHtmlAnchorElement>("a.lesson-contents__download-row")
                    .Select(m => m.Href).Distinct().ToList();

                var lessonContentTitle = document
                    .QuerySelectorAll<IHtmlAnchorElement>("a.lesson-contents__download-row")
                    .Select(m => m.Text).Distinct().ToList();

                //lessonContent.Url = document.QuerySelectorAll<IHtmlAnchorElement>("a.lesson-contents__download-row").Select(m => m.Href).Distinct().ToList();
                //var r = lessonContentUrl.Select((x, i) => new LessonContent { Url = x, Title = });

                lesson.LessonContents = lessonContentUrl
                    .Zip(lessonContentTitle, (u, t) => new LessonContent {Url = u, Title = t}).ToList();

                LessonsData.Add(lesson);
            }
        }


        public async Task GetDataCourse()
        {
            if (!Auth) return;

            string firstMentor = CourseMentors.FirstOrDefault();
            string courseStart = null;
            if (CourseStart != null)
            {
                courseStart = DateTime.Parse(CourseStart.Substring(0, 11)).ToString("yyyy.MM");
            }


            foreach (Lesson lesson in LessonsData)
            {
                string saveDir = null;
                StringBuilder materials = new StringBuilder();
                materials.AppendLine($"Название курса: {CourseTitle}");
                materials.AppendLine($"Название урока: {lesson.Title}");
                materials.AppendLine($"Ссылка: {lesson.Url}");
                materials.AppendLine($"Дата: {CourseStart}");
                materials.AppendLine();
                materials.AppendLine("Материалы:");

                //[GeekBrains][Geek Brains]
                //Не добавляем в saveDir если - Преподавательский состав, Автор курса: Geek Brains
                if (firstMentor == "Geek Brains") firstMentor = null;

                foreach (LessonContent content in lesson.LessonContents)
                {
                    saveDir = Path.Join(DownloadDir,
                        $"[GeekBrains]{(firstMentor is null ? "" : "[" + firstMentor + "]")} {CourseTitle}{(courseStart is null ? string.Empty : " (" + courseStart + ")")}",
                        lesson.Title);
                    Utils.CreateDir(saveDir);

                    materials.AppendLine(content.Title);
                    materials.AppendLine(content.Url);
                    materials.AppendLine();

                    string downloadUrl = content.Url;
                    string downloadFileName = content.Title;
                    string extensionFileName = GetExt(content.Url);

                    //IgnoreDownloadExt ToLower(

                    //Console.WriteLine(extensionFileName);

                    // if (!string.IsNullOrWhiteSpace(extensionFileName) && !IgnoreDownloadExt.Contains(extensionFileName
                    //     .ToLower()))
                    // {
                    //     Console.WriteLine("Download file");
                    //     Console.WriteLine(content.Url);
                    // }
                    // else
                    // {
                    //     Console.WriteLine("IgnoreDownloadExt");
                    //     Console.WriteLine(content.Url);
                    // }
                    //
                    // continue;


                    // if (Uri.TryCreate(content.Url, UriKind.Absolute, out var uri))
                    // {
                    //     Console.WriteLine(Path.GetExtension(uri.LocalPath));
                    //     extensionFileName = Path.GetExtension(uri.LocalPath);
                    // }

                    //https://docs.google.com/presentation/d/<FileID>/export/<format>
                    //https://docs.google.com/uc?export=download&format=pdf&id=YourIndividualID
                    //https://docs.google.com/document/d/15cpvE-TdUP1eZ92nyoBx5vmz0dwysWeusZcnqzNFd68/export?format=pdf
                    //https://docs.google.com/uc?export=download&format=pdf&id=15cpvE-TdUP1eZ92nyoBx5vmz0dwysWeusZcnqzNFd68

                    //https://docs.google.com/presentation/d/1onxOXgov-QadTDT3oAFaOyLX4IRQEtoq6UNuj253u9k/export/pdf
                    //https://docs.google.com/document/export?format=pdf&id=13wfIzCG8zjZHe9ZotFzAi_g0khMsbSOWjb7L_7fwpwY

                    //docs.google.com presentation
                    if (downloadUrl.ToLower().StartsWith("https://docs.google.com/presentation/"))
                    {
                        downloadUrl = downloadUrl.Replace("edit", "export/pdf");
                        downloadFileName = $"{content.Title}.pdf";
                        //Console.WriteLine($"Загрузка файла : {downloadUrl}");


                        await DownloadFile(downloadUrl, saveDir, downloadFileName);
                    }

                    //docs.google.com document
                    if (downloadUrl.ToLower().StartsWith("https://docs.google.com/document/"))
                    {
                        downloadUrl = downloadUrl.Replace("edit", "export?format=pdf");
                        downloadFileName = $"{content.Title}.pdf";
                        //Console.WriteLine(downloadUrl);

                        await DownloadFile(downloadUrl, saveDir, downloadFileName);
                    }


                    if (!string.IsNullOrWhiteSpace(extensionFileName) && !IgnoreDownloadExt.Contains(extensionFileName
                        .ToLower()))
                    {
                        downloadFileName = $"{content.Title}{extensionFileName}";
                        //Console.WriteLine(downloadUrl);
                        //Console.WriteLine(downloadFileName);

                        await DownloadFile(downloadUrl, saveDir, downloadFileName);
                    }


                    // if (downloadUrl.ToLower().EndsWith(".mp4"))
                    // {
                    //     //downloadFileName = $"{lesson.Title}.mp4";
                    //     downloadFileName = $"{content.Title}.mp4";
                    //     Console.WriteLine(downloadUrl);
                    //     Console.WriteLine(downloadFileName);
                    //
                    //     //await DownloadFile(downloadUrl, saveDir, downloadFileName);
                    // }

                    // if (downloadUrl.ToLower().EndsWith(".zip"))
                    // {
                    //     downloadFileName = $"{content.Title}.zip";
                    //     Console.WriteLine(downloadUrl);
                    //     Console.WriteLine(downloadFileName);
                    //
                    //     await DownloadFile(downloadUrl, saveDir, downloadFileName);
                    // }
                    //
                    // if (downloadUrl.ToLower().EndsWith(".rar"))
                    // {
                    //     downloadFileName = $"{content.Title}.rar";
                    //     Console.WriteLine(downloadUrl);
                    //     Console.WriteLine(downloadFileName);
                    //
                    //     //await DownloadFile(downloadUrl, saveDir, downloadFileName);
                    // }


                    // if (downloadFileName.ToLower().Equals("запись вебинара") && downloadUrl.ToLower().EndsWith(".mp4"))
                    // {
                    //     downloadUrl = downloadUrl.Replace("edit", "export?format=pdf");
                    //     downloadFileName = $"{lesson.Title}.mp4";
                    //     Console.WriteLine(downloadUrl);
                    //
                    //     await DownloadFile(downloadUrl, saveDir, downloadFileName);
                    // }


                    //var download = _context.GetService<IDocumentLoader>().FetchAsync(new DocumentRequest(new Url(downloadUrl)));

                    //using (var response = await download.Task)
                    //{
                    //    using (var target = File.OpenWrite(Path.Join(saveDir, downloadFileName)))
                    //    {
                    //        Console.WriteLine(Path.Join(saveDir, content.Title));
                    //        await response.Content.CopyToAsync(target);
                    //    }
                    //}
                }

                await File.WriteAllTextAsync(Path.Join(saveDir, LessonMaterials), materials.ToString());
            }
        }


        private string GetExt(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return Path.GetExtension(uri.LocalPath);
            }

            return null;
        }


        private async Task DownloadFile(string url, string path, string filename)
        {
            //GetFileName(url);
            //GetFileSize(url);

            if (url is null) return;
            if (path is null) return;

            var invalidChars = Path.GetInvalidPathChars();
            var validFilename = filename.Any(x => invalidChars.Contains(x));

            if (filename.Length > 16 || validFilename)
                filename = Path.GetRandomFileName().Replace(".", "") + GetExt(url);

            var pathToSave = Path.Join(path, filename);

            Console.WriteLine($"Загрузка файла : {url}");
            Console.WriteLine($"Сохранение файла : {pathToSave}");
            Console.WriteLine();

            IDownload download = _context.GetService<IDocumentLoader>().FetchAsync(new DocumentRequest(new Url(url)));

            using IResponse response = await download.Task;
            await using FileStream target = File.OpenWrite(pathToSave);
            await response.Content.CopyToAsync(target);
        }

        // private static string GetFileName(string url)
        // {
        //     HttpWebRequest request = (HttpWebRequest) WebRequest.Create(url);
        //     string fileName = null;
        //     string defaultFileName = "";
        //
        //     try
        //     {
        //         HttpWebResponse res = (HttpWebResponse) request.GetResponse();
        //         using (Stream rstream = res.GetResponseStream())
        //         {
        //             fileName = res.Headers["Content-Disposition"] != null
        //                 ? res.Headers["Content-Disposition"].Replace("attachment; filename=", "").Replace("\"", "")
        //                 : res.Headers["Location"] != null
        //                     ? Path.GetFileName(res.Headers["Location"])
        //                     : Path.GetFileName(url).Contains('?') || Path.GetFileName(url).Contains('=')
        //                         ? Path.GetFileName(res.ResponseUri.ToString())
        //                         : defaultFileName;
        //         }
        //
        //         res.Close();
        //     }
        //     catch
        //     {
        //         // ignored
        //     }
        //
        //     Console.WriteLine(fileName);
        //     return fileName;
        // }


        // private static string GetFileSize(string uriPath)
        // {
        //     WebRequest webRequest = HttpWebRequest.Create(uriPath);
        //     webRequest.Method = "HEAD";
        //
        //     using (var webResponse = webRequest.GetResponse())
        //     {
        //         string fileSize = webResponse.Headers.Get("Content-Length");
        //         double fileSizeInMegaByte = Math.Round(Convert.ToDouble(fileSize) / 1024.0 / 1024.0, 2);
        //
        //         Console.WriteLine(fileSizeInMegaByte + " MB");
        //
        //         return fileSizeInMegaByte + " MB";
        //     }
        // }


        private DefaultHttpRequester CreateRequester()
        {
            var requester = new AngleSharp.Io.DefaultHttpRequester();
            requester.Headers["User-Agent"] =
                "Mozilla/5.0 (Windows NT 6.1; Win64; x64; rv:77.0) Gecko/20100101 Firefox/77.0";
            return requester;
        }


        private IConfiguration CreateConfig()
        {
            return Configuration.Default.With(_requester).WithDefaultLoader().WithDefaultCookies();
        }


        private IBrowsingContext CreateContext()
        {
            return BrowsingContext.New(_config);
        }
    }
}