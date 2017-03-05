namespace CassiniDev
{
    public class ServerInfo
    {
        private int port;

        public ServerInfo(int port)
        {
            this.port = port;
        }

        public int Port
        {
            get
            {
                return port;
            }
        }
    }
}