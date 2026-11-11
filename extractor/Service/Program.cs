using FlexExtractor;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
namespace FlexExtractor.Service;
public static class Program {
 public static async Task<int> Main(string[] args){if(args.Contains("--self-test"))return SelfTests.Run("service");var builder=Host.CreateApplicationBuilder(args);builder.Environment.ContentRootPath=AppContext.BaseDirectory;builder.Services.AddWindowsService(o=>o.ServiceName="FlexRequestExtractor");builder.Services.AddHostedService<ServiceWorker>();await builder.Build().RunAsync();return Environment.ExitCode;}
}
public sealed class ServiceWorker:BackgroundService {
 Engine? engine;readonly Func<Config> load;
 public ServiceWorker():this(()=>Durable.LoadConfig(Path.Combine(AppContext.BaseDirectory,"config.json"))){}
 public ServiceWorker(Func<Config> load){this.load=load;}
 protected override async Task ExecuteAsync(CancellationToken stoppingToken){try{var config=load();engine=new Engine(config);engine.Start();try{while(!stoppingToken.IsCancellationRequested){if(!engine.Snapshot.Running)throw new IOException("引擎停止："+engine.Snapshot.Status);await Task.Delay(500,stoppingToken);}}catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){}finally{await engine.DisposeAsync();}}catch(Exception)when(!stoppingToken.IsCancellationRequested){Environment.ExitCode=1;throw;}}
}
