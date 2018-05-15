using System;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Receiver.DTO;
using Receiver.DeSerialization;
using System.Threading.Tasks;
using System.Threading;

class RPCServer
{
    private static List<ClientPropertise> ClientsList = new List<ClientPropertise>();
    public static void Main()
    {
        var factory = new ConnectionFactory() { HostName = "localhost" };
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
                    ClientsList.Add(new ClientPropertise() { qName = props.ReplyTo, Body = Encoding.UTF8.GetString(ea.Body), CorellationId = props.CorrelationId });
                }
                switch (ea.BasicProperties.CorrelationId)
                {
                    case ObjectCategory.Message:
                        Message mess = ea.Body.Deserializer<Message>();
                        var replyProps = channel.CreateBasicProperties();
                        replyProps.CorrelationId = props.CorrelationId;

                        foreach (ClientPropertise p in ClientsList)
                        {
                            if (props.ReplyTo != p.qName)
                                channel.BasicPublish(exchange: "", routingKey: p.qName, basicProperties: replyProps, body: ea.Body);
                        }

                        break;
                    case ObjectCategory.StatusInfo:
                        StatusInfo status = ea.Body.Deserializer<StatusInfo>();
                        return;
                        break;
                    default:
                        break;
                }


            };
            pingToAll();
            Console.WriteLine(" Press [enter] to exit.");
            Console.ReadLine();
        }
    }
    //протестить если один ушел в оффлайн. Но в коллекции он есть...
    private static void pingToAll()
    {
        Task.Run(() =>
        {
            while (true)
            {
                foreach (ClientPropertise c in ClientsList)
                {
                    c.ReplyTo//нет в коллекции
                }
                Thread.Sleep(5000);
            }
        });
    }
}