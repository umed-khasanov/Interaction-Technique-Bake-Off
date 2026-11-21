// Static class holding the participant prefix and number.
// Not attached to any GameObject. Persists between scenes.
public static class Session
{
    public static string ParticipantPrefix = "P"; // use "S" when testing in the simulator
    public static int ParticipantNumber = 1;

    public static string ParticipantId => $"{ParticipantPrefix}{ParticipantNumber:00}";
}
