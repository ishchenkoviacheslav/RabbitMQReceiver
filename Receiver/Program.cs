using System;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Receiver.DTO;
using System.Threading.Tasks;
using System.Threading;
using Receiver.DeSerialization;
using DTO;

class RPCServer
{
    private static List<ClientPropertise> ClientsList = new List<ClientPropertise>();

    public static void Main()
    {
        var factory = new ConnectionFactory() { UserName = "slavik", Password = "slavik", HostName = "localhost" };
        //var factory = new ConnectionFactory() { HostName = "localhost" };
        using (var connection = factory.CreateConnection())
        using (var channel = connection.CreateModel())
        {
            channel.QueueDeclare(queue: "rpc_queue", durable: false, exclusive: false, autoDelete: false, arguments: null);
            channel.BasicQos(0, 1, false);
            var consumer = new EventingBasicConsumer(channel);
            channel.BasicConsume(queue: "rpc_queue", autoAck: true, consumer: consumer);
            Console.WriteLine(" [x] Awaiting RPC requests");

            consumer.Received += (model, ea) =>
            {
                IBasicProperties props = ea.BasicProperties;
                if (ClientsList.Where((c) => c.qName == props.ReplyTo).Count() == 0)
                {
                    ClientsList.Add(new ClientPropertise() { qName = props.ReplyTo, CorellationId = props.CorrelationId, LastUpTime = DateTime.UtcNow });
                }
                switch (ea.BasicProperties.CorrelationId)
                {
                    case ObjectCategory.Message:
                        Message mess = ea.Body.Deserializer<Message>();
                        IBasicProperties replyProps = channel.CreateBasicProperties();
                        replyProps.CorrelationId = props.CorrelationId;
                        foreach (ClientPropertise p in ClientsList)
                        {
                            if (props.ReplyTo != p.qName)
                                channel.BasicPublish(exchange: "", routingKey: p.qName, basicProperties: replyProps, body: mess.Serializer());
                        }

                        break;
                    case ObjectCategory.StatusInfo:
                        StatusInfo status = ea.Body.Deserializer<StatusInfo>();
                        ClientsList.ForEach((c) =>
                        {
                            if (c.qName == ea.BasicProperties.ReplyTo)
                            {
                                c.LastUpTime = DateTime.UtcNow;
                            }
                        });
                        
                        return;
                        break;
                    default:
                        break;
                }
            };
            PingToAll(channel);
            Console.WriteLine(" Press [enter] to exit.");
            Console.ReadLine();
        }
    }
    //протестить если один ушел в оффлайн.Но в коллекции он есть...
    private static void PingToAll(IModel channel)
    {
        Task.Run(() =>
        {
            while (true)
            {
                foreach (ClientPropertise c in ClientsList)
                {
                    IBasicProperties prop = channel.CreateBasicProperties();
                    prop.CorrelationId = ObjectCategory.StatusInfo;
                    channel.BasicPublish(exchange: "", routingKey: c.qName, basicProperties: prop, body: null);
                }
                ClientsList.RemoveAll((c) =>
                {
                    return DateTime.UtcNow.Subtract(c.LastUpTime) > new TimeSpan(0, 0, 30);
                });
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Green;
                ClientsList.ForEach((c) => Console.WriteLine(c.qName));
                Console.WriteLine(ClientsList.Count);
                Thread.Sleep(5000);
            }
        });
    }
}