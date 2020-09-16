import os, base64, json, requests
from flask import Flask, render_template, request, flash

# Define Cognitive Services variables
vision_key = 'df50e40297694a1cbf39b2687444a519'
vision_endpoint = 'https://westus.api.cognitive.microsoft.com/'
translator_key = '4f58c28438164e75892d64a08e951de7'
translator_endpoint = 'https://api.cognitive.microsofttranslator.com/'

app = Flask(__name__)
app.secret_key = os.urandom(24)

@app.route("/", methods=["GET", "POST"])
def index():
    language="en"

    if request.method == "POST":
        # Display the image that was uploaded
        image = request.files["file"]
        uri = "data:;base64," + base64.b64encode(image.read()).decode("utf-8")
        image.seek(0)

        # Use the Computer Vision API to extract text from the image
        lines = extract_text(vision_endpoint, vision_key, image)
        
        language = request.form["language"]
        # Use the Translator API to translate text extracted from the image
        translated_lines = translate_text(translator_endpoint, translator_key, lines, language)

        # Flash the translated text
        for translated_line in translated_lines:
            flash(translated_line)

    else:
        # Display a placeholder image
        uri = "/static/placeholder.png"

    return render_template("index.html", image_uri=uri, language=language)

# Function that extracts text from images
def extract_text(endpoint, key, image):
    uri = endpoint + 'vision/v3.0/ocr'

    headers = {
        'Ocp-Apim-Subscription-Key': key,
        'Content-type': 'application/octet-stream'
    }

    try:
        response = requests.post(uri, headers=headers, data=image)
        response.raise_for_status() # Raise exception if call failed
        results = response.json()
        
        lines=[]

        if len(results['regions']) == 0:
            lines.append('Photo contains no text to translate')

        else:
            for line in results['regions'][0]['lines']:
                text = ' '.join([word['text'] for word in line['words']])
                lines.append(text)

        return lines

    except requests.exceptions.HTTPError as e:
        return ['Error calling the Computer Vision API: ' + e.strerror]

    except Exception as e:
        return ['Error calling the Computer Vision API']

# Function that translates text into the specified language
def translate_text(endpoint, key, lines, language):
    uri = endpoint + 'translate?api-version=3.0&to=' + language

    headers = {
        'Ocp-Apim-Subscription-Key': key,
        'Content-type': 'application/json'
    }

    input=[]

    for line in lines:
        input.append({ "text": line })

    try:
        response = requests.post(uri, headers=headers, json=input)
        response.raise_for_status() # Raise exception if call failed
        results = response.json()

        translated_lines = []

        for result in results:
            for translated_line in result["translations"]:
                translated_lines.append(translated_line["text"])

        return translated_lines

    except requests.exceptions.HTTPError as e:
        return ['Error calling the Translator Text API: ' + e.strerror]

    except Exception as e:
        return ['Error calling the Translator Text API']