using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Landscape.Config;
using DCL.Landscape.Utils;
using System.Threading;
using UnityEngine.AddressableAssets;

namespace DCL.Landscape.Parcel
{
    public class LandscapeParcelController
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly LandscapeParcelService parcelService;
        private readonly LandscapeParcelData landscapeParcelData;

        public LandscapeParcelController(
            IAssetsProvisioner assetsProvisioner,
            LandscapeParcelService parcelService,
            LandscapeParcelData landscapeParcelData
        )
        {
            this.assetsProvisioner = assetsProvisioner;
            this.parcelService = parcelService;
            this.landscapeParcelData = landscapeParcelData;
        }

        public async UniTask InitializeAsync(AssetReferenceT<ParcelData> parsedParcels, CancellationToken ct)
        {
            ProvidedAsset<ParcelData> parcelData = await assetsProvisioner.ProvideMainAssetAsync(parsedParcels, ct);
            FetchParcelResult fetchParcelResult = await parcelService.LoadManifestAsync(ct);

            if (!fetchParcelResult.Succeeded)
                landscapeParcelData.Reconfigure(
                    parcelData.Value.GetRoadParcels(),
                    parcelData.Value.GetOwnedParcels(),
                    parcelData.Value.GetEmptyParcels()
                );
            else
                landscapeParcelData.Reconfigure(
                    fetchParcelResult.Manifest.GetRoadParcels(),
                    fetchParcelResult.Manifest.GetOccupiedParcels(),
                    fetchParcelResult.Manifest.GetEmptyParcels()
                );
        }
    }
}
