#ifndef PARTICLE_DATA_INCLUDED
#define PARTICLE_DATA_INCLUDED

struct ParticleData
{
    float3 position;
    float3 velocity;
    float4 color;
    uint isAlive;
    uint fraction;
    float health;
};

#endif
