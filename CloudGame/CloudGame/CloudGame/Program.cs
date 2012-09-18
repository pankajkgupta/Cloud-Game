using System;

namespace CloudGame
{
#if WINDOWS || XBOX
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            using (CloudGame game = new CloudGame())
            {
                game.Run();
            }
        }
    }
#endif
}

