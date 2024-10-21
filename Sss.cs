private static void AdjustOtherSamplePositions(
    IEnumerable<Sample> allSamplesForPositionUpdate,
    Dictionary<string, int> tempPositionMap,
    int newPosition,
    int oldPosition)
{
    bool isMovingDown = newPosition < oldPosition;

    // Get samples in the position range between old and new position
    var positionRange = allSamplesForPositionUpdate
        .Where(s => isMovingDown
            ? s.Position >= newPosition && s.Position < oldPosition
            : s.Position <= newPosition && s.Position > oldPosition);

    // Adjust positions for samples in the range
    foreach (var item in positionRange)
    {
        // If the sample is not in tempPositionMap, increment by 2 or decrement by 2
        if (!tempPositionMap.ContainsKey(item.Code))
        {
            tempPositionMap[item.Code] = item.Position + (isMovingDown ? 2 : -2);
        }
        else
        {
            // If it is already in the map, increment or decrement by 1
            tempPositionMap[item.Code] = item.Position + (isMovingDown ? 1 : -1);
        }
    }
}
