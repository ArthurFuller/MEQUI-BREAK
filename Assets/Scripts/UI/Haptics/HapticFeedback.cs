// Compatibility cleanup file.
//
// An earlier development build created a second global HapticFeedback class at
// this path. The project already has its own HapticFeedback implementation under
// Assets/Scripts/UI/Interaction, so this file intentionally defines NO TYPES.
//
// It is kept temporarily so extracting this project over an older working copy
// overwrites the stale duplicate script and resolves CS0101.
