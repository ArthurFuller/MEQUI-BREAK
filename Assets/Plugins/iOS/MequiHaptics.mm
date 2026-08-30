#import <UIKit/UIKit.h>

extern "C"
{
    void MequiHapticSelection()
    {
        if (@available(iOS 10.0, *))
        {
            UISelectionFeedbackGenerator *generator = [[UISelectionFeedbackGenerator alloc] init];
            [generator prepare];
            [generator selectionChanged];
        }
    }

    void MequiHapticLightImpact()
    {
        if (@available(iOS 10.0, *))
        {
            UIImpactFeedbackGenerator *generator =
                [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];
            [generator prepare];
            [generator impactOccurred];
        }
    }

    void MequiHapticConfirm()
    {
        if (@available(iOS 10.0, *))
        {
            UIImpactFeedbackGenerator *generator =
                [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleMedium];
            [generator prepare];
            [generator impactOccurred];
        }
    }

    void MequiHapticReject()
    {
        if (@available(iOS 10.0, *))
        {
            UINotificationFeedbackGenerator *generator =
                [[UINotificationFeedbackGenerator alloc] init];
            [generator prepare];
            [generator notificationOccurred:UINotificationFeedbackTypeError];
        }
    }

    void MequiHapticSuccess()
    {
        if (@available(iOS 10.0, *))
        {
            UINotificationFeedbackGenerator *generator =
                [[UINotificationFeedbackGenerator alloc] init];
            [generator prepare];
            [generator notificationOccurred:UINotificationFeedbackTypeSuccess];
        }
    }
}
