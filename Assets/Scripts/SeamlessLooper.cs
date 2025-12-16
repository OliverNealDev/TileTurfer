using UnityEngine;

public class SeamlessLooper : MonoBehaviour
{
    [SerializeField] private AudioClip introClip;
    [SerializeField] private AudioClip loopClip;
    [Range(0f, 1f)] [SerializeField] private float volume = 0.5f;

    private AudioSource introSource;
    private AudioSource loopSource;

    void Start()
    {
        introSource = gameObject.AddComponent<AudioSource>();
        loopSource = gameObject.AddComponent<AudioSource>();

        introSource.clip = introClip;
        loopSource.clip = loopClip;

        introSource.volume = volume;
        loopSource.volume = volume;

        introSource.playOnAwake = false;
        loopSource.playOnAwake = false;

        loopSource.loop = true;

        double startTime = AudioSettings.dspTime + 0.1;
        double introDuration = (double)introClip.samples / introClip.frequency;

        introSource.PlayScheduled(startTime);
        loopSource.PlayScheduled(startTime + introDuration);
    }
}