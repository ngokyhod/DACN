namespace DACS.Services
{
    using System.Diagnostics;

    
        public class PythonBridge
        {
            public static string RunPython(string script, string args)
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "python", // hoặc "python3" nếu bạn cài Python 3
                    Arguments = $"{script} {args}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                using var reader = process.StandardOutput;
                string result = reader.ReadToEnd();
                process.WaitForExit();

                return result;
            }
        }
    }


