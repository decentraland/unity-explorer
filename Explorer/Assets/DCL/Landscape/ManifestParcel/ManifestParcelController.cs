using Cysharp.Threading.Tasks;
using DCL.Landscape.Utils;
using System.Threading;
using Unity.Collections;

namespace DCL.Landscape.ManifestParcel
{
    public class ManifestParcelController
    {
        private readonly LandscapeParcelService parcelService;
        private readonly ManifestParcelData manifestParcelData;

        public ManifestParcelController(LandscapeParcelService parcelService, ManifestParcelData manifestParcelData)
        {
            this.parcelService = parcelService;
            this.manifestParcelData = manifestParcelData;
        }

        public async UniTask InitializeAsync(CancellationToken ct)
        {
            FetchParcelResult fetchParcelResult = await parcelService.LoadManifestAsync(ct);

            if (!fetchParcelResult.Succeeded)
                return;

            manifestParcelData.Reconfigure(
                fetchParcelResult.Manifest.GetRoadParcels(),
                fetchParcelResult.Manifest.GetOccupiedParcels(),
                fetchParcelResult.Manifest.GetEmptyParcels()
            );
        }
    }
}
