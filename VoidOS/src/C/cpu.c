#include <stdint.h>

static char cpu_brand[49] = {0};
static int brand_loaded = 0;

char* get_cpu_brand() {
    if (!brand_loaded) {
        brand_loaded = 1;

        unsigned int max_ext;
        asm volatile("cpuid" : "=a"(max_ext) : "a"(0x80000000) : "ebx", "ecx", "edx");

        if (max_ext >= 0x80000004) {
            unsigned int regs[12];

            for (int i = 0; i < 3; i++) {
                asm volatile("cpuid"
                    : "=a"(regs[i*4]), "=b"(regs[i*4+1]),
                      "=c"(regs[i*4+2]), "=d"(regs[i*4+3])
                    : "a"(0x80000002 + i));
            }

            int pos = 0;
            for (int i = 0; i < 12 && pos < 48; i++) {
                unsigned int val = regs[i];
                for (int j = 0; j < 4 && pos < 48; j++) {
                    cpu_brand[pos++] = (char)(val & 0xFF);
                    val >>= 8;
                }
            }
            cpu_brand[pos] = '\0';
        }
    }

    return cpu_brand;
}