FROM python:3.9-slim

WORKDIR /app

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY . .

# KIS API standard port
EXPOSE 9443

# Run without SSL for simplicity in demo-local, 
# or use a proxy like Nginx to handle SSL
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "9443"]
