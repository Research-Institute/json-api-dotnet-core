<p align="center">
<img src ="https://raw.githubusercontent.com/json-api-dotnet/JsonApiDotnetCore/master/logo.png" />
</p>

# JsonApiDotNetCore
A framework for building [JSON:API](http://jsonapi.org/) compliant REST APIs using .NET Core and Entity Framework Core. Includes support for [Atomic Operations](https://jsonapi.org/ext/atomic/).

[![Build](https://ci.appveyor.com/api/projects/status/t8noo6rjtst51kga/branch/master?svg=true)](https://ci.appveyor.com/project/json-api-dotnet/jsonapidotnetcore/branch/master)
[![Coverage](https://codecov.io/gh/json-api-dotnet/JsonApiDotNetCore/branch/master/graph/badge.svg?token=pn036tWV8T)](https://codecov.io/gh/json-api-dotnet/JsonApiDotNetCore)
[![NuGet](https://img.shields.io/nuget/v/JsonApiDotNetCore.svg)](https://www.nuget.org/packages/JsonApiDotNetCore/)
[![Chat](https://badges.gitter.im/json-api-dotnet-core/Lobby.svg)](https://gitter.im/json-api-dotnet-core/Lobby?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)
[![FIRST-TIMERS](https://img.shields.io/badge/first--timers--only-friendly-blue.svg)](http://www.firsttimersonly.com/)

The ultimate goal of this library is to eliminate as much boilerplate as possible by offering out-of-the-box features such as sorting, filtering and pagination. You just need to focus on defining the resources and implementing your custom business logic. This library has been designed around dependency injection, making extensibility incredibly easy.

## Getting Started

These are some steps you can take to help you understand what this project is and how you can use it:

- [What is JSON:API and why should I use it?](https://nordicapis.com/the-benefits-of-using-json-api/)
- [The JSON:API specification](http://jsonapi.org/format/)
- Demo [Video](https://youtu.be/KAMuo6K7VcE), [Blog](https://dev.to/wunki/getting-started-5dkl)
- [Our documentation](https://www.jsonapi.net/)
- [Check out the example projects](https://github.com/json-api-dotnet/JsonApiDotNetCore/tree/master/src/Examples)
- [Embercasts: Full Stack Ember with ASP.NET Core](https://www.embercasts.com/course/full-stack-ember-with-dotnet/watch/whats-in-this-course-cs)
- [Roadmap](ROADMAP.md)

## Related Projects

- [Performance Reports](https://github.com/json-api-dotnet/PerformanceReports)
- [JsonApiDotNetCore.MongoDb](https://github.com/json-api-dotnet/JsonApiDotNetCore.MongoDb)
- [JsonApiDotNetCore.Marten](https://github.com/wayne-o/JsonApiDotNetCore.Marten)
- [Todo List App](https://github.com/json-api-dotnet/TodoListExample)

## Examples

See the [examples](https://github.com/json-api-dotnet/JsonApiDotNetCore/tree/master/src/Examples) directory for up-to-date sample applications. There is also a [Todo List App](https://github.com/json-api-dotnet/TodoListExample) that includes a JsonApiDotNetCore API and an EmberJs client.

## Installation and Usage

See [our documentation](https://www.jsonapi.net/) for detailed usage.

### Models

```c#
public class Article : Identifiable
{
    [Attr]
    public string Name { get; set; }
}
```

### Controllers

```c#
public class ArticlesController : JsonApiController<Article>
{
    public ArticlesController(IJsonApiOptions options, ILoggerFactory loggerFactory,
        IResourceService<Article> resourceService,)
        : base(options, loggerFactory, resourceService)
    {
    }
}
```

### Middleware

```c#
public class Startup
{
    public IServiceProvider ConfigureServices(IServiceCollection services)
    {
        services.AddJsonApi<AppDbContext>();
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseJsonApi();
        app.UseEndpoints(endpoints => endpoints.MapControllers());
    }
}
```

## Compatibility

A lot of changes were introduced in v4. The following chart should help you pick the best version, based on your environment.
See also our [versioning policy](./VERSIONING_POLICY.md).

| .NET Version      | EF Core Version | JsonApiDotNetCore Version |
| ----------------- | --------------- | ------------------------- |
| .NET Core 2.x     | 2.x             | v3.x                      |
| .NET Core 3.1     | 3.1, 5          | v4.x                      |
| .NET 5            | 5               | v4.x, v5.x                |


## Contributing

Have a question, found a bug or want to submit code changes? See our [contributing guidelines](./.github/CONTRIBUTING.md).

## Trying out the latest build

After each commit to the master branch, a new prerelease NuGet package is automatically published to AppVeyor at https://ci.appveyor.com/nuget/jsonapidotnetcore. To try it out, follow the next steps:

* In Visual Studio: **Tools**, **NuGet Package Manager**, **Package Manager Settings**, **Package Sources**
    * Click **+**
    * Name: **AppVeyor JADNC**, Source: **https://ci.appveyor.com/nuget/jsonapidotnetcore**
    * Click **Update**, **Ok**
* Open the NuGet package manager console (**Tools**, **NuGet Package Manager**, **Package Manager Console**)
    * Select **AppVeyor JADNC** as package source
    * Run command: `Install-Package JonApiDotNetCore -pre`

## Development

To build the code from this repository locally, run:

```bash
dotnet build
```

Running tests locally requires access to a PostgreSQL database. If you have docker installed, this can be propped up via:

```bash
run-docker-postgres.ps1
```

And then to run the tests:

```bash
dotnet test
```

Alternatively, to build and validate the code, run all tests, generate code coverage and produce the NuGet package:

```bash
Build.ps1
```
