using System;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.IO.Pipelines;
using System.Buffers;

namespace HostedServiceTest
{
    class Program
    {
        static void Main(string[] args)
        {
            var hostBuilder = new HostBuilder()
                .ConfigureServices(services => {
                    services.AddMediatR();
                    services.AddHostedService<MyHostedService>();
                })
                .ConfigureLogging(logging => {
                    logging.AddConsole();
                });


            hostBuilder.RunConsoleAsync().Wait();
        }
    }

    internal class MyHostedService : BackgroundService
    {
        ILogger _logger;
        IMediator _mediator;

        public MyHostedService(ILogger<MyHostedService> logger, IMediator mediator)
        {
            _logger = logger;
            _mediator = mediator;    
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("sending message");
                await _mediator.Send(new PingCommand());
                await Task.Delay(2000);
            }
        }
    }

    internal class PingCommand : IRequest
    {
        
    }

    internal class PingHandler : IRequestHandler<PingCommand>
    {
        ILogger _logger;

        public PingHandler(ILogger<PingHandler> logger)
        {
            _logger = logger;    
        }
        public Task<Unit> Handle(PingCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Ping handled");
            return Task.FromResult(new Unit());
        }
    }

    public class AddressInUseException : Exception 
    {
        public AddressInUseException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    internal class SocketTransport
    {
        private Socket _socket;

        public Func<SocketConnection,Task> OnConnected {get;set;}

        public Task StartAsync(CancellationToken stoppingToken)
        {
            var endPoint = new IPEndPoint(IPAddress.Any, 6000);
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            
            try
            {
            _socket.Bind(endPoint);
            }
            catch (SocketException e) when (e.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                throw new AddressInUseException(e.Message, e);
            }

            _socket.Listen(512);
            
            return Task.CompletedTask;
        }

        private async Task AcceptLoopAsync() 
        {
            try
            {
                while (true)
                {
                    try
                    {
                        var acceptSocket = await _socket.AcceptAsync();
                        var connection = new SocketConnection(acceptSocket);
                        _ = HandleConnectionAsync(connection);
                    }
                    catch (SocketException)
                    {                       
                    }
                }
            }
            catch (Exception e)
            {                 
            }
        }

        private async Task HandleConnectionAsync(SocketConnection connection)
        {
            try
            {
                var handlerTask = OnConnected(connection);
                var transportTask = connection.StartAsync();

                await transportTask;
                await handlerTask;

                connection.Dispose();
            }
            catch (Exception e)
            {

            }
        }
    }

    internal class SocketConnection : IDisposable
    {
        public IDuplexPipe Transport { get; set; }
        public IDuplexPipe Application { get; set; }

        public PipeWriter Input => Application.Output;
        public PipeReader Output => Application.Input;

        public SocketConnection(Socket socket)
        {
            
        }

        public Task StartAsync()
        {

            return Task.CompletedTask;
        }

        public void Dispose()
        {}
    }
}
