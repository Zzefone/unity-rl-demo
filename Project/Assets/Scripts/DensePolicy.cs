using UnityEngine;
[System.Serializable]
public class DenseLayer
{
    public int inputSize,outputSize;
    public float[] weights,bias;
    public bool tanh;
    public float[] Forward(float[] x)
    {
        var y=new float[outputSize];for(int j=0;j<outputSize;j++){float v=bias[j];for(int i=0;i<inputSize;i++)v+=weights[j*inputSize+i]*x[i];y[j]=tanh?(float)System.Math.Tanh(v):v;}return y;
    }
}
[System.Serializable]
public class DensePolicy
{
    public DenseLayer[] layers;
    public Vector2 Predict(float[] observation)
    {
        var x=observation;foreach(var layer in layers)x=layer.Forward(x);return new Vector2(Mathf.Clamp(x[0],-1,1),Mathf.Clamp(x[1],-1,1));
    }
}
