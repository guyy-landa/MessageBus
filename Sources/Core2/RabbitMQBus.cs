﻿using System;
using System.Collections.Generic;

using MessageBus.Core.API;
using RabbitMQ.Client;

namespace MessageBus.Core
{
    public class RabbitMQBus : IBus
    {
        private readonly IConnection _connection;

        private readonly IMessageHelper _messageHelper = new MessageHelper();

        private readonly string _exchange;

        public RabbitMQBus() : this(Guid.NewGuid().ToString()) { }

        public RabbitMQBus(string busId) : this(busId, new RabbitMQConnectionString())
        {

        }

        public RabbitMQBus(string busId, Uri uri) : this(busId, new RabbitMQConnectionString(uri))
        {
            
        }

        public RabbitMQBus(string busId, RabbitMQConnectionString connectionString)
        {
            BusId = busId;

            _exchange = "amq.headers";
            string username = "guest";
            string password = "guest";
            
            if (!string.IsNullOrEmpty(connectionString.Endpoint))
            {
                _exchange = connectionString.Endpoint;
            }
            
            if (!string.IsNullOrEmpty(connectionString.Username))
            {
                username = connectionString.Username;
            }

            if (!string.IsNullOrEmpty(connectionString.Password))
            {
                password = connectionString.Password;
            }
            
            ConnectionFactory factory = new ConnectionFactory
            {
                HostName = connectionString.Host,
                Port = connectionString.Port,
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
                UserName = username,
                Password = password,
                VirtualHost = connectionString.VirtualHost
            };

            _connection = factory.CreateConnection();
        }

        public string BusId { get; private set; }
        
        public void Dispose()
        {
            _connection.Dispose();
        }
        
        public IPublisher CreatePublisher(Action<IPublisherConfigurator> configure = null)
        {
            PublisherConfigurator configuration = new PublisherConfigurator();

            if (configure != null)
            {
                configure(configuration);
            }

            IModel model = _connection.CreateModel();

            return new Publisher(model, BusId, _exchange, configuration, _messageHelper);
        }

        public IReceiver CreateReceiver(Action<ISubscriberConfigurator> configure = null)
        {
            SubscriberConfigurator configurator = CreateConfigurator(configure);

            IModel model = _connection.CreateModel();

            QueueDeclareOk queue = CreateQueue(model, configurator);

            return new Receiver(model, BusId, _exchange, queue.QueueName, _messageHelper, configurator.Serializers, configurator.ErrorSubscriber, configurator.ReceiveSelfPublish);
        }

        public ISubscriber CreateSubscriber(Action<ISubscriberConfigurator> configure = null)
        {
            SubscriberConfigurator configurator = CreateConfigurator(configure);

            IModel model = _connection.CreateModel();

            QueueDeclareOk queue = CreateQueue(model, configurator);

            IMessageConsumer consumer = new MessageConsumer(model, BusId, _messageHelper, configurator.Serializers, configurator.ErrorSubscriber, configurator.TaskScheduler, configurator.ReceiveSelfPublish);

            return new Subscriber(model, _exchange, queue, consumer, configurator.ReceiveSelfPublish);
        }

        public ISubscription RegisterSubscription<T>(T instance, Action<ISubscriberConfigurator> configure = null)
        {
            SubscriberConfigurator configurator = CreateConfigurator(configure);

            IModel model = _connection.CreateModel();

            QueueDeclareOk queue = CreateQueue(model, configurator);

            IMessageConsumer consumer = new MessageConsumer(model, BusId, _messageHelper, configurator.Serializers, configurator.ErrorSubscriber, configurator.TaskScheduler, configurator.ReceiveSelfPublish);

            return new Subscription(model, _exchange, queue, consumer, instance, configurator.ReceiveSelfPublish);
        }

        private static SubscriberConfigurator CreateConfigurator(Action<ISubscriberConfigurator> configure)
        {
            SubscriberConfigurator configurator = new SubscriberConfigurator();

            if (configure != null)
            {
                configure(configurator);
            }

            return configurator;
        }

        private static QueueDeclareOk CreateQueue(IModel model, SubscriberConfigurator configurator)
        {
            string queueName = configurator.QueueName;

            bool durable = !string.IsNullOrEmpty(queueName);
            bool exclusive = !durable;
            bool autoDelete = !durable;

            QueueDeclareOk queueDeclare = model.QueueDeclare(queueName, durable, exclusive, autoDelete, new Dictionary<string, object>());

            return queueDeclare;
        } 
    }
}