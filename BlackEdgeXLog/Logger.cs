namespace BlackEdgeCommon.Utils.Logging
{

    public class Logger
    {
        static Logger _logger = new Logger();
        public static Logger GetLogger()
        {
            return _logger;
        }

        public void Info(string msg)
        {
        }

    }
}
