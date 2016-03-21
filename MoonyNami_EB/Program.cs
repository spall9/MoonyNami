using EloBuddy.SDK.Events;

namespace MoonyNami_EB
{
    class Program
    {
        static void Main(string[] args)
        {
            Loading.OnLoadingComplete += eventArgs => new Nami();
        }
    }
}
