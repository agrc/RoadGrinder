using ESRI.ArcGIS.Geodatabase;

namespace RoadGrinder.contracts
{
    public interface IGrindable
    {
        void Grind(IWorkspace output);
        IWorkspace CreateOutput();
    }
}