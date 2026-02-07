import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  async rewrites() {
    return [
      {
        source: "/api/:path*",
        destination: "http://localhost:5051/api/:path*",
      },
      {
        source: "/hubs/:path*",
        destination: "http://localhost:5051/hubs/:path*",
      },
    ];
  },
};

export default nextConfig;
