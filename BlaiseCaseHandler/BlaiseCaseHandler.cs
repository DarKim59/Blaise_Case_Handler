using System.ServiceProcess;
using System.Text;
using System.IO;
using System.Security;
using StatNeth.Blaise.API.ServerManager;
using RabbitMQ.Client;
using System.Web.Script.Serialization;
using RabbitMQ.Client.Events;
using System.Configuration;
using System;
using StatNeth.Blaise.API.DataLink;
using StatNeth.Blaise.API.Meta;
using DataRecordAPI = StatNeth.Blaise.API.DataRecord;
using System.Collections.Generic;
using log4net;
using log4net.Config;

namespace BlaiseCaseHandler
{

    /// <summary>
    /// Case class represents the dataset we're injesting from RabbitMQ and aids us in the
    /// deserialisation process as it provides the structure the message needs to fit.
    /// </summary>
    public class Case
    {
        public string serial_number { get; set; }
        public string source_hostname { get; set; }
        public string source_server_park { get; set; }
        public string source_instrument { get; set; }
        public string dest_hostname { get; set; }
        public string dest_server_park { get; set; }
        public string dest_instrument { get; set; }
        public string action { get; set; }
    }

    public partial class BlaiseCaseHandler : ServiceBase
    {

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public IConnection connection;
        public IModel channel;
        public EventingBasicConsumer consumer;
        public List<string> queueNames;

        /// <summary>
        /// Class constructor for initialising the service.
        /// </summary>        
        public BlaiseCaseHandler()
        {
            InitializeComponent();
        }

        /// <summary>
        /// This method is our entry point when debugging. It allows us to use the service without running the installation steps.
        /// </summary>
        public void OnDebug()
        {
            OnStart(null);
        }

        /// <summary>
        /// OnStart method triggers when a service beings.
        /// </summary>
        /// <param name="args">optional arguments that can be passed on service start.</param>
        protected override void OnStart(string[] args)
        {
            log.Info("Blaise Case Handler service started.");



            // Sets up the connection to the Rabbit queue and stores the required objects 
            // into the BlaiseCaseHandler class variables.
            SetupRabbitConnection();

            // Start the ReadQueue function. This will pull messages from the Rabbit queue 
            // whenever they appear and process them accordingly.
            ReadQueue();
        }

        /// <summary>
        /// OnStop method triggers when the service is being stopped.
        /// </summary>
        protected override void OnStop()
        {
            log.Info("Blaise Case Handler service stopped.");
        }

        /// <summary>
        /// This method contains our service's core functionality. The content includes the RabbitMQ 
        /// channel's consumption method and what to do with any messages consumed.
        /// </summary>
        public void ReadQueue()
        {
            IDataLink4 dl_source = null;
            IDatamodel dm_source = null;
            IDataLink4 dl_dest = null;
            IDatamodel dm_dest = null;

            // We specify the functionality to be performed when a message is consumed.
            consumer.Received += (model, ea) =>
            {

                // Extract the message body and encode it into the UTF8 format.
                var body = ea.Body;
                var message = Encoding.UTF8.GetString(body);

                log.Info("Message received - " + message);

                Case data = null;
                try
                {
                    // Take the serialized JSON string and deserialize it into a Case object.
                    data = new JavaScriptSerializer().Deserialize<Case>(message);

                    dl_source = GetDataLink(data.source_hostname, data.source_instrument, data.source_server_park);
                    dm_source = dl_source.Datamodel;

                    dl_dest = GetDataLink(data.dest_hostname, data.dest_instrument, data.dest_server_park);
                    dm_dest = dl_dest.Datamodel;

                    // Use a key object to check if the case being moved exists.
                    var key = DataRecordAPI.DataRecordManager.GetKey(dm_source, "PRIMARY");
                    key.Fields[0].DataValue.Assign(data.serial_number);

                    // If the key does not exist in our database then move/copy it.
                    if (dl_source.KeyExists(key))
                    {
                        var record = dl_source.ReadRecord(key);

                        //{"serial_number":"1500","source_hostname":"bsp-d-001.ukwest.cloudapp.azure.com","source_server_park":"Telephone-Live","source_instrument":"OPN1901A","dest_hostname":"DESKTOP-0OF1LSJ","dest_server_park":"LocalDevelopment","dest_instrument":"OPN1901A","action":"copy"}


                        if (data.action == "copy")
                        {
                            dl_dest.Write(record);
                            SendStatus(MakeStatusJson(data, "Case Copied"));
                        }

                        if (data.action == "move")
                        {
                            dl_dest.Write(record);
                            dl_source.Delete(key);
                            SendStatus(MakeStatusJson(data, "Case Moved"));
                        }

                    }
                    else
                    {
                        log.Error("Case " + data.serial_number.ToString() + " doesn't exist in database. Aborted.");
                        SendStatus(MakeStatusJson(data, "Case NOT Found"));
                    }
                }
                catch (Exception e)
                {
                    log.Error(e);
                    SendStatus(MakeStatusJson(data, "Error - " + e));
                }

                // Remove from queue when done processing
                channel.BasicAck(ea.DeliveryTag, false);

            };

            string queueName = ConfigurationManager.AppSettings["HandlerQueueName"];
            // This function instructs our consumer object to process the messages received through the given queue name.
            channel.BasicConsume(queue: queueName,
                    autoAck: false,
                    consumer: consumer);
            
        }

        /// <summary>
        /// Method for setting up the RabbitMQ connection objects.
        /// </summary>
        public void SetupRabbitConnection()
        {
            log.Info("Setting up RabbitMQ connection.");

            // Create a connection to RabbitMQ using the Rabbit credentials stored in the app.config file.
            var factory = new ConnectionFactory()
            {
                HostName = ConfigurationManager.AppSettings["RabbitHostName"],
                UserName = ConfigurationManager.AppSettings["RabbitUserName"],
                Password = ConfigurationManager.AppSettings["RabbitPassword"]
            };
            connection = factory.CreateConnection();
            channel = connection.CreateModel();

            // Get the Exchange details from the app.config file.
            string exchangeName = ConfigurationManager.AppSettings["RabbitExchange"];
            string queueName = ConfigurationManager.AppSettings["HandlerQueueName"];

            // Declare an exchange which our queue will use when receiving messages. 
            channel.ExchangeDeclare(exchange: exchangeName, type: "direct", durable: true);
            log.Info("Exchange declared - " + exchangeName);

            // Declare handler queue
            channel.QueueDeclare(queue: queueName,
                                durable: true,
                                exclusive: false,
                                autoDelete: false,
                                arguments: null);
            log.Info("Queue declared - " + queueName);

            // Bind handler queue
            channel.QueueBind(queue: queueName,
                              exchange: exchangeName,
                              routingKey: queueName);
            log.Info("Queue binding complete - " + queueName + " / " + exchangeName + " / " + queueName);

            // Declare case status queue
            string caseStatusQueueName = ConfigurationManager.AppSettings["CaseStatusQueueName"];
            channel.QueueDeclare(queue: caseStatusQueueName,
                                 durable: true,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: null);

            // Setting for only consuming one message at a time.
            channel.BasicQos(0, 1, false);

            // Create the consumer object which will run our code when receiving messages.
            consumer = new EventingBasicConsumer(channel);
            log.Info("Consumer object created.");

            log.Info("RabbitMQ setup complete.");
        }





        /// <summary>
        /// Function for generating the Datalink object used to create and update data.
        /// </summary>
        /// <param name="instrumentName"> The name of the target instrument (survey).</param>
        /// <param name="serverPark">The server park where the survey is installed.</param>
        /// <returns> IDataLink4 object for the connected server park.</returns>
        public static IDataLink4 GetDataLink(string hostname, string instrumentName, string serverPark)
        {

            // Use default credentials (assuming Blaise developer installation):
            string userName = ConfigurationManager.AppSettings["BlaiseServerUserName"];
            string password = ConfigurationManager.AppSettings["BlaiseServerPassword"];
            int port = 8031;

            if (hostname == "DESKTOP-0OF1LSJ")
            {
                password = "Root";
            }

            Guid instrument_id = Guid.NewGuid();
            try
            {
                // Get the instrument guid from the server.
                IConnectedServer connServer2 = ServerManager.ConnectToServer(hostname, port, userName, GetPassword(password));

                bool found = false;
                foreach (ISurvey survey in connServer2.GetServerPark(serverPark).Surveys)
                {
                    if (survey.Name == instrumentName)
                    {
                        instrument_id = survey.InstrumentID;
                        found = true;
                    }
                }
                if (!found)
                    throw new System.ArgumentOutOfRangeException(String.Format("Instrument: {0} not found on server park: {1}", instrumentName, serverPark));

                // Connect to the server
                IRemoteDataServer connServer = DataLinkManager.GetRemoteDataServer(hostname, 8033, userName, GetPassword(password));

                return connServer.GetDataLink(instrument_id, serverPark);
            }
            catch (Exception e)
            {
                log.Error(e.Message);
                log.Error(e.StackTrace);
                return null;
            }
        }











        /// <summary>
        /// Converts a password string to a Secure string format.
        /// </summary>
        /// <param name="pw">a password string to be converted to the secure string format.</param>
        /// <returns></returns>
        private static SecureString GetPassword(string pw)
        {
            char[] passwordChars = pw.ToCharArray();
            SecureString password = new SecureString();
            foreach (char c in passwordChars)
            {
                password.AppendChar(c);
            }
            return password;
        }


        /// <summary>
        /// Builds a json status object to be used in the Send Status function
        /// </summary>
        /// <param name="caseData"> Case object containing case information.</param>
        /// <param name="status"> Status of the case being processed.</param>
        /// <returns>Json object containing required information.</returns>
        private Dictionary<string, string> MakeStatusJson(Case caseData, string status)
        {
            Dictionary<string, string> jsonData = new Dictionary<string, string>();

            jsonData["case_id"] = caseData.serial_number;
            jsonData["instrument_name"] = caseData.source_hostname;
            jsonData["instrument_name"] = caseData.source_server_park;
            jsonData["server_park"] = caseData.source_instrument;
            jsonData["instrument_name"] = caseData.dest_hostname;
            jsonData["serial_number"] = caseData.dest_server_park;
            jsonData["issue_number"] = caseData.dest_instrument;
            jsonData["interviewer_id"] = caseData.action;
            jsonData["status"] = status;
            jsonData["service_name"] = ConfigurationManager.AppSettings["ServiceName"];

            return jsonData;
        }


        /// <summary>
        /// Sends a summary message to RabbitMQ 
        /// </summary>
        private void SendStatus(Dictionary<string, string> jsonData)
        {
            string message = new JavaScriptSerializer().Serialize(jsonData);
            var body = Encoding.UTF8.GetBytes(message);
            string caseStatusQueueName = ConfigurationManager.AppSettings["CaseStatusQueueName"];
            channel.BasicPublish(exchange: "",
                        routingKey: caseStatusQueueName,
                        body: body);
            log.Info("Message sent to RabbitMQ status_update queue : " + message);
        }
    }
}
