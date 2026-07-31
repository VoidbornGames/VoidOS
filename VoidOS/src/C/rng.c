#include <stdint.h>

int rng_have_rdrand(void) {
    unsigned int eax = 1, ebx = 0, ecx = 0, edx = 0;
    __asm__ __volatile__(
        "cpuid"
        : "+a"(eax), "=b"(ebx), "=c"(ecx), "=d"(edx)
        :
        : "memory"
    );
    return (ecx & (1U << 30)) ? 1 : 0;
}

int rng_rdrand16(unsigned char* out) {
    if (!rng_have_rdrand()) return 0;

    for (int half = 0; half < 2; half++) {
        unsigned long long val = 0;
        int ok = 0;
        for (int retry = 0; retry < 10; retry++) {
            unsigned char cf;
            __asm__ __volatile__(
                "rdrand %1\n\t"
                "setc %0"
                : "=q"(cf), "=r"(val)
                :
                : "cc"
            );
            if (cf) { ok = 1; break; }

            __asm__ __volatile__("pause" ::: "memory");
        }
        if (!ok) return 0;

        for (int i = 0; i < 8; i++) {
            out[half * 8 + i] = (unsigned char)(val & 0xFF);
            val >>= 8;
        }
    }
    return 1;
}

unsigned long long read_tsc(void) {
    unsigned int lo, hi;
    __asm__ __volatile__("rdtsc" : "=a"(lo), "=d"(hi));
    return ((unsigned long long)hi << 32) | lo;
}