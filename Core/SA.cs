using Microsoft.Z3;

namespace OlliSaarikivi.Sre
{
    class SA
    {
        internal struct Transition
        {
            int target;
            Expr guard, update;
        }
        
        MultiValueIntDictionary<Transition> transitions;
    }
}
