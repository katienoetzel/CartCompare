using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace CartCompare.Api;

// Drops controllers named *DevelopmentController (provider smoke tests
// and store bootstrapping) from routing, so they answer 404.
public class RemoveDevelopmentControllersConvention
    : IApplicationModelConvention
{
    public void Apply(
        ApplicationModel application)
    {
        application.Controllers
            .Where(controller =>
                controller.ControllerType.Name.EndsWith(
                    "DevelopmentController",
                    StringComparison.Ordinal
                )
            )
            .ToList()
            .ForEach(controller =>
                application.Controllers.Remove(controller)
            );
    }
}
