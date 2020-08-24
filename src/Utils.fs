module Utils

let debug format =
#if RELEASE
#else
    printf format
#endif