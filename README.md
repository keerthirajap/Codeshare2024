private static void AdjustOtherSamplePositions(
    IEnumerable<Sample> allSamplesForPositionUpdate,
    Dictionary<string, int> tempPositionMap,
    int newPosition,
    int oldPosition,
    Sample updatedSample)
{
    // Exclude the updated sample from the list and order the rest by position
    var remainingSamples = allSamplesForPositionUpdate
        .Where(s => s.Code != updatedSample.Code)
        .OrderBy(s => s.Position)
        .ToList();

    // Insert the updated sample into its new position in the list
    remainingSamples.Insert(newPosition - 1, updatedSample);

    // Recalculate the positions of all samples in the list, adjusting the map
    for (int i = 0; i < remainingSamples.Count; i++)
    {
        var sample = remainingSamples[i];
        tempPositionMap[sample.Code] = i + 1;  // Set the new position (1-based index)
    }
}
