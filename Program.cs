using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;


namespace SpaceMissionControl
{
    class Program
    {
        static string senderEmail;
        static string password;

        static void Main(string[] args)
        {
            if (args.Length != 4)
            {
                Console.WriteLine("Usage: SpaceMissionControl <folder> <senderEmail> <password> <receiverEmail>");
                return;
            }

            string folderPath = args[0];
            senderEmail = args[1];
            password = args[2];
            string receiverEmail = args[3];

            List<WeatherData> weatherDataList = new List<WeatherData>();

            foreach (var filePath in Directory.GetFiles(folderPath, "*.csv"))
            {
                var weatherData = ReadWeatherData(filePath);
                weatherDataList.Add(weatherData);
            }

            var bestDatesByLocation = FindBestLaunchDatesByLocation(weatherDataList);
            var bestCombination = FindBestCombination(bestDatesByLocation);
            GenerateLaunchAnalysisReport(bestCombination, "LaunchAnalysisReport.csv", receiverEmail);

            Console.WriteLine("Launch Analysis Report generated successfully!");

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static List<(string Spaceport, WeatherDay BestLaunchDate)> FindBestLaunchDatesByLocation(List<WeatherData> weatherDataList)
        {
            var bestDatesByLocation = new List<(string Spaceport, WeatherDay BestLaunchDate)>();

            foreach (var data in weatherDataList)
            {
                WeatherDay bestDay = null;

                foreach (var day in data.WeatherByDay)
                {
                    if (IsSuitableForLaunch(day))
                    {
                        if (bestDay == null || day.Temperature < bestDay.Temperature)
                        {
                            bestDay = day;
                        }
                    }
                }

                if (bestDay != null)
                {
                    bestDatesByLocation.Add((data.Location, bestDay));
                }
            }

            return bestDatesByLocation;
        }

        static (string Spaceport, WeatherDay BestLaunchDate) FindBestCombination(List<(string Spaceport, WeatherDay BestLaunchDate)> bestDatesByLocation)
        {
            if (bestDatesByLocation.Count == 0)
            {
                return (null, null);
            }

            // Sort by temperature (ascending)
            var sortedByTemperature = bestDatesByLocation.OrderBy(x => x.BestLaunchDate.Temperature).ToList();

            // Find the first location with the lowest temperature
            var bestCombination = sortedByTemperature.First();

            return bestCombination;
        }

        static WeatherData ReadWeatherData(string filePath)
        {
            var lines = File.ReadAllLines(filePath);
            var data = new WeatherData
            {
                Location = Path.GetFileNameWithoutExtension(filePath)
            };

            // Start from the second line (skipping the header)
            for (int i = 1; i < lines.Length; i++)
            {
                var columns = lines[i].Split(',');

                // Ensure there are enough columns
                if (columns.Length >= 2)
                {
                    if (int.TryParse(columns[1].Trim(), out int temperature))
                    {
                        var day = i; // Day number corresponds to the line number
                        var wind = int.Parse(columns[2].Trim()); // Assuming wind and other values are integers as well
                        var humidity = int.Parse(columns[3].Trim());
                        var precipitation = int.Parse(columns[4].Trim());
                        var lightning = columns[5].Trim() == "Yes";
                        var clouds = columns[6].Trim();

                        data.WeatherByDay.Add(new WeatherDay
                        {
                            Day = day,
                            Temperature = temperature,
                            Wind = wind,
                            Humidity = humidity,
                            Precipitation = precipitation,
                            Lightning = lightning,
                            Clouds = clouds,
                            Location = data.Location
                        });
                    }
                    else
                    {
                        // Log or handle invalid temperature value
                        Console.WriteLine($"Invalid temperature value at line {i + 1}: {columns[1].Trim()}");
                    }
                }
                else
                {
                    // Log or handle insufficient columns in the row
                    Console.WriteLine($"Invalid data format at line {i + 1}: {lines[i]}");
                }
            }

            return data;
        }


        static bool IsSuitableForLaunch(WeatherDay day)
        {
            return day.Temperature >= 28 && day.Temperature <= 32 &&
                   day.Wind <= 10 &&
                   day.Humidity <= 60 &&
                   day.Precipitation == 0 &&
                   !day.Lightning &&
                   (day.Clouds == "Cumulus" || day.Clouds == "Cirrus");
        }

        static void GenerateLaunchAnalysisReport((string Spaceport, WeatherDay BestLaunchDate) bestCombination, string filePath, string receiverEmail)
        {
            

            // Create CSV file
            using (StreamWriter writer = new StreamWriter(filePath, append: true))
            {
                // Write the header if empty
                if (new FileInfo(filePath).Length == 0)
                {
                    writer.WriteLine("Spaceport,Best Launch Date");
                }

                // Write the best combination to the CSV file
                writer.WriteLine($"{bestCombination.Spaceport},{bestCombination.BestLaunchDate.Day}");
            }

            // Send the report
            SendEmail(receiverEmail, "Launch Analysis Report", $"Best launch date for {bestCombination.Spaceport}: {bestCombination.BestLaunchDate.Day}", filePath);
        }

        static void SendEmail(string receiverEmail, string subject, string body, string attachmentFilePath)
        {
            try
            {
                // Create the log entry before attaching the file
                string logFilePath = "E:\\SpaceMissionControl\\TXT\\EmailLog.txt";
                using (StreamWriter logWriter = new StreamWriter(logFilePath, append: true))
                {
                    logWriter.WriteLine("Date: " + DateTime.Now);
                    logWriter.WriteLine("To: " + receiverEmail);
                    logWriter.WriteLine("Subject: " + subject);
                    logWriter.WriteLine("Body: " + body);
                    logWriter.WriteLine("Attachment: " + attachmentFilePath);
                    logWriter.WriteLine("------------------------------------");
                }

                // Create the email message
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(senderEmail, senderEmail));
                message.To.Add(new MailboxAddress(receiverEmail, receiverEmail));
                message.Subject = subject;

                var bodyBuilder = new BodyBuilder { TextBody = body };

                // Attach the file
                bodyBuilder.Attachments.Add(attachmentFilePath);

                message.Body = bodyBuilder.ToMessageBody();

                // Configure the SMTP client
                using (var client = new SmtpClient())
                {
                    client.Connect("smtp-mail.outlook.com", 587, SecureSocketOptions.StartTls); // Replace with your SMTP server and port
                    client.Authenticate(senderEmail, password);

                    // Send the email
                    client.Send(message);
                    client.Disconnect(true);

                    Console.WriteLine("Email sent successfully.");
                }
            }
            catch (SmtpCommandException ex)
            {
                Console.WriteLine($"SMTP Error: {ex.StatusCode} - {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"Error: Unauthorized Access - {ex.Message}");
            }
            catch (DirectoryNotFoundException ex)
            {
                Console.WriteLine($"Error: Directory Not Found - {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }






    }
}
