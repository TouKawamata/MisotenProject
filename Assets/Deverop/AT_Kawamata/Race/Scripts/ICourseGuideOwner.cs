// 自分のコースへの問い合わせ口（ICourseGuide）を持つレーサー。分岐のあるコースでは、レーサーごとに走っている区間が違うため、
// 進行度（RaceProgressTracker）・通常の接触（RacerContactResolver）は、これを実装していればその人のガイドを使う。
// 実装していないレーサー（DummyRacerなど）は、今まで通り本線のCourseSplineで扱う。
public interface ICourseGuideOwner
{
    // Initialize前はnullでもよい（そのときは本線で扱う）。
    ICourseGuide CourseGuide { get; }
}
