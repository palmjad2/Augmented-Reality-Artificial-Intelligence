import socket
import numpy as np
from scipy.optimize import minimize

def superquadric_inside_out(params, points):
    a1, a2, a3, e1, e2, tx, ty, tz, rx, ry, rz = params
    
    # Clamp to avoid numerical issues
    a1, a2, a3 = max(a1, 0.01), max(a2, 0.01), max(a3, 0.01)
    e1, e2 = np.clip(e1, 0.1, 2.0), np.clip(e2, 0.1, 2.0)

    # Translate points
    pts = points - np.array([tx, ty, tz])

    # Rotate points (simple Euler XYZ)
    cx, sx = np.cos(rx), np.sin(rx)
    cy, sy = np.cos(ry), np.sin(ry)
    cz, sz = np.cos(rz), np.sin(rz)

    Rx = np.array([[1,0,0],[0,cx,-sx],[0,sx,cx]])
    Ry = np.array([[cy,0,sy],[0,1,0],[-sy,0,cy]])
    Rz = np.array([[cz,-sz,0],[sz,cz,0],[0,0,1]])
    R = Rz @ Ry @ Rx

    pts = (R.T @ pts.T).T

    x, y, z = pts[:,0], pts[:,1], pts[:,2]

    # Superquadric inside-outside function
    F = ((np.abs(x/a1)**(2/e2) + np.abs(y/a2)**(2/e2))**(e2/e1) + np.abs(z/a3)**(2/e1)) - 1

    return np.sum(F**2)

def fit_superquadric(points):
    # Initial guess: bounding box center and half-sizes
    center = points.mean(axis=0)
    half = (points.max(axis=0) - points.min(axis=0)) / 2

    x0 = [half[0], half[1], half[2],  # a1, a2, a3
           1.0, 1.0,                    # e1, e2 (1.0 = cylinder-ish)
           center[0], center[1], center[2],  # tx, ty, tz
           0.0, 0.0, 0.0]              # rx, ry, rz

    result = minimize(
        superquadric_inside_out,
        x0,
        args=(points,),
        method='Nelder-Mead',
        options={'maxiter': 5000, 'xatol': 1e-4, 'fatol': 1e-4}
    )

    return result.x

def start_server():
    HOST = '127.0.0.1'
    PORT = 65432

    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        s.bind((HOST, PORT))
        s.listen()
        print(f"Fitter listening on {HOST}:{PORT}")

        while True:
            conn, addr = s.accept()
            with conn:
                print(f"Connected by {addr}")
                data = b""
                while True:
                    chunk = conn.recv(4096)
                    if not chunk:
                        break
                    data += chunk

                # Parse points
                text = data.decode('utf-8').strip()
                lines = text.strip().split('\n')
                points = []
                for line in lines:
                    vals = line.strip().split(',')
                    if len(vals) == 3:
                        points.append([float(v) for v in vals])

                points = np.array(points)
                print(f"Received {len(points)} points. Fitting...")

                params = fit_superquadric(points)

                # Send back 11 parameters
                response = ','.join([f"{p:.6f}" for p in params])
                conn.sendall(response.encode('utf-8'))
                print(f"Sent params: {response}")

if __name__ == "__main__":
    start_server()
