using System;
using MiniSplitter.Services;

namespace MiniSplitter
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                // Start the bot service
                var botService = new BotService();

                // Keep the application running
                Console.WriteLine("Bot is running. Press Enter to exit.");
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Main: {ex.Message}");
            }
        }
    }
}
