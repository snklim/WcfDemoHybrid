using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.ServiceModel;
using System.Runtime.Serialization;

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
                string serverEndpoint = Settings.DefaultServerEndpoint;

                if (string.IsNullOrEmpty(serverEndpoint)) serverEndpoint = defUrl;
                defUrl = serverEndpoint;

                try
                {
                    using (var serverHost = new ServiceHost(typeof(SimpleService), new Uri(serverEndpoint)))
                    {
                        serverHost.Open();

                        Console.WriteLine("Server started at " + serverEndpoint);

                        serverCreated = true;
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

            Console.ReadLine();
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

            var server = ChannelFactory<ISimpleService>.CreateChannel(
                            new BasicHttpBinding(),
                            new EndpointAddress(Settings.DefaultServerEndpoint));

            var clientService = new SimpleClient();
            var url = Settings.DefaultClientEndpoint + "_" + clientService.ServiceUniqueName.ToString();

            server.RegisterClient(new ClientDescription
            {
                ClientId = clientService.ServiceUniqueName,
                ClientName = clientName,
                ServiceClietnCallbackUrl = url
            });

            using (var clientHost = new ServiceHost(clientService, new Uri(url)))
            {
                clientHost.Open();

                while (true)
                {
                    string msg = Console.ReadLine();
                    server.SendMessage(clientService.ServiceUniqueName, msg);
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
            string hostName = "localhost:8081";
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
            Console.WriteLine("User \"{0}\" joined", clientDescr.ClientName);
            _clientsDescr[clientDescr.ClientId] = clientDescr;
            _clients[clientDescr.ClientId] = ChannelFactory<ISimpleClient>.CreateChannel(
                                new BasicHttpBinding(),
                                new EndpointAddress(clientDescr.ServiceClietnCallbackUrl));
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
