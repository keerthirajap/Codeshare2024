private static void AdjustOtherSamplePositions(
    IEnumerable<Sample> allSamplesForPositionUpdate,
    Dictionary<string, int> tempPositionMap,
    int newPosition,
    int oldPosition,
    IEnumerable<Sample> updatedSamples)
{
    // Get the codes of all updated samples
    var updatedSampleCodes = updatedSamples.Select(s => s.Code).ToHashSet();

    // Exclude the updated samples from the list and order the remaining by position
    var remainingSamples = allSamplesForPositionUpdate
        .Where(s => !updatedSampleCodes.Contains(s.Code))
        .OrderBy(s => s.Position)
        .ToList();

    // Insert each updated sample at the appropriate new position
    foreach (var updatedSample in updatedSamples)
    {
        remainingSamples.Insert(newPosition - 1, updatedSample);  // Insert updated sample at new position
        newPosition++;  // Increment new position for subsequent inserts
    }

    // Recalculate the positions of all samples in the list, adjusting the map
    for (int i = 0; i < remainingSamples.Count; i++)
    {
        var sample = remainingSamples[i];
        tempPositionMap[sample.Code] = i + 1;  // Set the new position (1-based index)
    }
}
