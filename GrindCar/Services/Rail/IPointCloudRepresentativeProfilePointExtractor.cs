using System.Collections.Generic;
using GrindCar.Models.Rail;
using GrindCar.Services.Rail.Processing;

namespace GrindCar.Services.Rail;

internal interface IPointCloudRepresentativeProfilePointExtractor
{
    MedianSectionExtractionResult ExtractMedianSectionProfileFromPoints(
        IReadOnlyList<PointCloudPoint3D> points,
        PointCloudDeviceSide side);
}
