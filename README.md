# Codeshare2024
Codeshare
var positionRange = isMovingDown
    ? allSamplesForPositionUpdate.Where(s => s.Position >= newPosition && s.Position < oldPosition)
                                 .OrderByDescending(s => s.Position)
    : allSamplesForPositionUpdate.Where(s => s.Position <= newPosition && s.Position > oldPosition)
                                 .OrderBy(s => s.Position);
                                 
