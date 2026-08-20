#import <UIKit/UIKit.h>

extern "C" void CarPrototype_PlayLightHaptic(float intensity)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (@available(iOS 13.0, *))
        {
            UIImpactFeedbackGenerator *generator =
                [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];
            [generator prepare];
            [generator impactOccurredWithIntensity:MAX(0.05f, MIN(1.0f, intensity))];
        }
        else if (@available(iOS 10.0, *))
        {
            UIImpactFeedbackGenerator *generator =
                [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];
            [generator prepare];
            [generator impactOccurred];
        }
    });
}
