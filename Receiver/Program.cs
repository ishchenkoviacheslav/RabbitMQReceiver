using System;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Collections.Generic;
using System.Linq;

class ClientPropertise
{
    public string qName { get; set; }
    public string Body { get; set; }
    public string CorellationId { get; set; }

}
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
                string messageBody = Encoding.UTF8.GetString(ea.Body);               
                Console.WriteLine(messageBody);
                IBasicProperties props = ea.BasicProperties;
                var replyProps = channel.CreateBasicProperties();
                replyProps.CorrelationId = props.CorrelationId;
                if (ClientsList.Where((c) => c.qName == props.ReplyTo).Count() == 0)
                    ClientsList.Add(new ClientPropertise() { qName = props.ReplyTo, Body = messageBody, CorellationId = props.CorrelationId });
                if (messageBody == "ping")
                    return;
                foreach (ClientPropertise p in ClientsList)
                {
                    if (props.ReplyTo != p.qName)
                        channel.BasicPublish(exchange: "", routingKey: p.qName, basicProperties: replyProps, body: ea.Body);
                }

            };

            Console.WriteLine(" Press [enter] to exit.");
            Console.ReadLine();
        }
    }
}