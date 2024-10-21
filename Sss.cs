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
        if (tempPositionMap.ContainsKey(item.Code))
            continue;  // Skip samples already in the tempPositionMap

        // Increment or decrement by 2 for samples not in tempPositionMap
        tempPositionMap[item.Code] = item.Position + (isMovingDown ? 2 : -2);
    }
}
