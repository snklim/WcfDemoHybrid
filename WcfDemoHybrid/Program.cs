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
            if(args.Length==0 || (args[0] != "-server" && args[0] != "-client"))
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
            using (var serverHost = new ServiceHost(typeof(SimpleService), new Uri("http://localhost/simpleService")))
            {
                serverHost.Open();

                Console.WriteLine("Server Started");

                Console.ReadLine();
            }
        }

        private static void RunClient()
        {
            string clientName = String.Empty;

            do
            {
                Console.Write("Enter your name:");
                clientName = Console.ReadLine();
            } while (String.IsNullOrEmpty(clientName));

            var server = ChannelFactory<ISimpleService>.CreateChannel(
                            new BasicHttpBinding(),
                            new EndpointAddress("http://localhost/simpleService"));

            var clientService = new SimpleClient();
            var url = "http://localhost/simpleClient" + "_" + clientService.ServiceUniqueName.ToString();

            server.RegisterClient(new ClientDescription { ClientId = clientService.ServiceUniqueName, ServiceClietnCallbackUrl = url });

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

    [DataContract]
    public class ClientDescription
    {
        [DataMember]
        public string ClientId { get; set; }
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

        public string ServiceUniqueName { get { return _serviceName; } }

        public SimpleService()
        {
            _serviceName = Guid.NewGuid().ToString();
            _clients = new Dictionary<string, ISimpleClient>();
        }

        public void SendMessage(string clientId, string message)
        {
            Console.WriteLine(clientId + ": " + message);
            foreach (var id in _clients.Keys)
            {
                if (id == clientId) continue;
                _clients[id].SendMessage(id, message);
            }
        }

        public void RegisterClient(ClientDescription clientDescr)
        {
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
