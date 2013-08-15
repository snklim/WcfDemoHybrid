using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.ServiceModel;
using System.Runtime.Serialization;
using System.Net;

namespace WcfDemoHybrid
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0 || (args[0] != "-server" && args[0] != "-client"))
            {
                Console.WriteLine("Please use \"-server\" or \"-client\" as a parameter");
                return;
            }

            if (args[0] == "-server")
            {
                RunServer();
            }
            else if (args[0] == "-client")
            {
                RunClient();
            }
        }

        private static void RunServer()
        {
            bool serverCreated = false;
            string defUrl = Settings.DefaultServerEndpoint;

            do
            {
                Console.Write("Enter server's endpoint (defailt is \"{0}\"):", defUrl);
                string serverEndpoint = Console.ReadLine();

                if (string.IsNullOrEmpty(serverEndpoint)) serverEndpoint = defUrl;
                defUrl = serverEndpoint;

                try
                {
                    using (var serverHost = new ServiceHost(typeof(SimpleService), new Uri(serverEndpoint)))
                    {
                        serverHost.Open();

                        Console.WriteLine("Server started at " + serverEndpoint);

                        Console.ReadLine();

                        return;
                    }
                }
                catch (Exception ex)
                {
                    Exception currEx = ex;
                    string msg = ex.Message;

                    while (currEx.InnerException != null)
                    {
                        currEx = currEx.InnerException;
                        msg += Environment.NewLine + currEx.Message;
                    }

                    Console.WriteLine("Error: \"{0}\". Please try again.", msg);
                }
            } while (!serverCreated);
        }

        private static void RunClient()
        {
            string clientName = String.Empty;

            do
            {
                Console.Write("Enter your name:");
                clientName = Console.ReadLine();
                if (string.IsNullOrEmpty(clientName)) Console.WriteLine("Empty name is not allowed. Please try again");
            } while (String.IsNullOrEmpty(clientName));

            var clientService = new SimpleClient();
            var defaultClientEndpoint = Settings.DefaultClientEndpoint + "_" + clientService.ServiceUniqueName.ToString();

            Console.Write("Enter client's endpoint (default is {0}):", defaultClientEndpoint);
            string clientEndpoint = Console.ReadLine();
            if (string.IsNullOrEmpty(clientEndpoint)) clientEndpoint = defaultClientEndpoint;

            using (var clientHost = new ServiceHost(clientService, new Uri(clientEndpoint)))
            {
                clientHost.Open();

                bool connectedToServer = false;
                ISimpleService server = null;
                int numOfAttempts = 10;

                do
                {
                    string defaultServerEndpoint = Settings.DefaultServerEndpoint;
                    Console.Write("Enter service's endpoint (default is {0}):", defaultServerEndpoint);

                    string serverEndpoint = Console.ReadLine();
                    if (string.IsNullOrEmpty(serverEndpoint)) serverEndpoint = defaultServerEndpoint;

                    server = ChannelFactory<ISimpleService>.CreateChannel(
                                            new BasicHttpBinding(),
                                            new EndpointAddress(serverEndpoint));

                    try
                    {
                        server.RegisterClient(new ClientDescription
                        {
                            ClientId = clientService.ServiceUniqueName,
                            ClientName = clientName,
                            ServiceClietnCallbackUrl = clientEndpoint
                        });
                        connectedToServer = true;
                    }
                    catch (Exception)
                    {
                        numOfAttempts--;
                        if (numOfAttempts > 0) Console.WriteLine("Cannot connect to server. Try again.");
                    }
                } while (!connectedToServer && numOfAttempts > 0);


                if (connectedToServer)
                {
                    while (true)
                    {
                        string msg = Console.ReadLine();
                        try
                        {
                            server.SendMessage(clientService.ServiceUniqueName, msg);
                        }
                        catch (Exception) { Console.WriteLine("Server shut down"); break; }
                    }
                }
                else
                {
                    Console.WriteLine("Cannot connect to server.");
                }
            }
        }
    }

    public static class Settings
    {
        public static readonly string DefaultServerEndpoint;
        public static readonly string DefaultClientEndpoint;

        static Settings()
        {
            string hostName = Dns.GetHostName() + ":8081";
            DefaultServerEndpoint = "http://" + hostName + "/simpleServer";
            DefaultClientEndpoint = "http://" + hostName + "/simpleClient";
        }
    }

    [DataContract]
    public class ClientDescription
    {
        [DataMember]
        public string ClientId { get; set; }
        [DataMember]
        public string ClientName { get; set; }
        [DataMember]
        public string ServiceClietnCallbackUrl { get; set; }
    }

    [ServiceContract]
    public interface ISimpleService
    {
        [OperationContract(IsOneWay=true)]
        void SendMessage(string clientId, string message);

        [OperationContract(IsOneWay=true)]
        void RegisterClient(ClientDescription clientDescr);
    }

    [ServiceBehavior(InstanceContextMode=InstanceContextMode.Single)]
    public class SimpleService : ISimpleService
    {
        string _serviceName;
        
        Dictionary<string, ISimpleClient> _clients;
        Dictionary<string, ClientDescription> _clientsDescr;

        public string ServiceUniqueName { get { return _serviceName; } }

        public SimpleService()
        {
            _serviceName = Guid.NewGuid().ToString();
            _clients = new Dictionary<string, ISimpleClient>();
            _clientsDescr = new Dictionary<string, ClientDescription>();
        }

        public void SendMessage(string clientId, string message)
        {
            Console.WriteLine(_clientsDescr[clientId].ClientName + ": " + message);
            foreach (var id in _clients.Keys)
            {
                if (id == clientId) continue;
                _clients[id].SendMessage(_clientsDescr[id].ClientName, message);
            }
        }

        public void RegisterClient(ClientDescription clientDescr)
        {
            string message = string.Format("User \"{0}\" joined", clientDescr.ClientName);

            _clientsDescr[clientDescr.ClientId] = clientDescr;
            _clients[clientDescr.ClientId] = ChannelFactory<ISimpleClient>.CreateChannel(
                                new BasicHttpBinding(),
                                new EndpointAddress(clientDescr.ServiceClietnCallbackUrl));

            SendMessage(clientDescr.ClientId, message);
        }
    }

    [ServiceContract]
    public interface ISimpleClient
    {
        [OperationContract(IsOneWay=true)]
        void SendMessage(string clientId, string message);
    }

    [ServiceBehavior(InstanceContextMode=InstanceContextMode.Single)]
    public class SimpleClient : ISimpleClient
    {
        private string _clientName;

        public string ServiceUniqueName { get { return _clientName; } }

        public SimpleClient()
        {
            _clientName = Guid.NewGuid().ToString();
        }

        public void SendMessage(string clientId, string message)
        {
            Console.WriteLine(clientId + ": " + message);
        }
    }

}
