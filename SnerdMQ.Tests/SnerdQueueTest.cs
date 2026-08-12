using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace SnerdMQ.Tests
{
    public class SnerdQueueTest
    {
        [Fact]
        public async Task TestEndToEndExecution()
        {
            Console.WriteLine("🚀 Booting up C# SnerdMQ Test App...");

            string osName = System.Runtime.InteropServices.RuntimeInformation.OSDescription.ToLower();
            string ext = osName.Contains("win") ? ".exe" : "";
            
            // Adjust the path so it points to the absolute local rust daemon
            string localBinary = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../snerdmq/target/debug/snerdmq" + ext));

            string dbFile = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../.snerdata/tasks/tasks.log"));
            if (File.Exists(dbFile))
            {
                File.Delete(dbFile);
            }

            using (var queue = new SnerdQueue(localBinary))
            {
                // TaskCompletionSource is the C# equivalent of a CountDownLatch
                var tcs = new TaskCompletionSource<bool>();

                queue.RegisterHandler("test_csharp_job", (json) =>
                {
                    Console.WriteLine($"\n✅ C# App received job! Data: {json}");
                    if (json.Contains("Anders Hejlsberg"))
                    {
                        tcs.SetResult(true);
                    }
                    else
                    {
                        tcs.SetException(new Exception("Assertion failed: message did not match"));
                    }
                });

                queue.StartListening();
                
                await Task.Delay(100);

                Console.WriteLine("Enqueuing job to Rust daemon...");
                queue.Enqueue(
                    "csharp-job-1",
                    "test_csharp_job",
                    "{\"user_id\":\"csharp_master\",\"message\":\"Anders Hejlsberg\"}",
                    3,
                    0.0
                );

                // Wait up to 5 seconds for the background ThreadPool to process the job
                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(5000));
                
                Assert.True(completedTask == tcs.Task, "The background job did not complete in time!");
                Console.WriteLine("🎉 Job processed successfully. Shutting down.");
            }
        }
    }
}
