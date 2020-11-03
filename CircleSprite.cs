using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
//离线生成圆环 图片 缓存在预制体中
public class CircleSprite : MonoBehaviour
{
    //空白区域色值
    [Header("空白区域色值")]
    public Color emptyColor = new Color(0, 0, 0, 0);
    //圆环区域色值
    [Header("圆环区域色值")]
    public Color circleColor = Color.white;
    //圆环内径/外径
    [Header("圆环内径")]
    public int minRadius = 40;
    [Header("圆环外径[圆形尺寸]")]
    public int maxRadius = 50;
    //扇形角度
    [Header("扇形角度")]
    public float circleAngle = 90;

    Color lastColor = Color.black;
    Color lastCircleColor = Color.black;
    int lastMinRadius = 0;
    int lastMaxRadius = 0;
    float lastCircleAngle = 0;

    private void Update()
    {
        if (Application.isPlaying) return;
        if (lastColor != emptyColor || lastCircleColor != circleColor || minRadius != lastMinRadius || maxRadius != lastMaxRadius || circleAngle != lastCircleAngle || circleAngle != lastCircleAngle)
        {
            circleAngle = Mathf.Clamp(circleAngle, 0, 361);
            minRadius = minRadius > maxRadius ? maxRadius : minRadius;
            lastColor = emptyColor;
            lastCircleColor = circleColor;
            lastMinRadius = minRadius;
            lastMaxRadius = maxRadius;
            lastCircleAngle = circleAngle;
            var sprite = CreateSprite(minRadius, maxRadius, circleAngle / 2, circleColor);
            sprite.name = "cirlce";
            this.GetComponent<Image>().sprite = sprite;
        }
    }


    /// <summary>
    /// 绘制扇形圆环，生成Sprite
    /// </summary>
    /// <param name="minRadius">圆环内径，值为0即是实心圆</param>
    /// <param name="maxRadius">圆环外径</param>
    /// <param name="circleAngle">1/2扇形弧度，值>=180度即是整园</param>
    Sprite CreateSprite(int minRadius, int maxRadius, float halfAngle, Color circleColor)
    {
        //图片尺寸
        int spriteSize = maxRadius * 2;
        //创建Texture2D
        Texture2D texture2D = new Texture2D(spriteSize, spriteSize);
        //图片中心像素点坐标
        Vector2 centerPixel = new Vector2(spriteSize / 2, spriteSize / 2);
        //
        Vector2 tempPixel;
        float tempAngle, tempDisSqr;
        if (halfAngle > 0 && halfAngle < 360)
        {
            //遍历像素点，绘制扇形圆环
            for (int x = 0; x < spriteSize; x++)
            {
                for (int y = 0; y < spriteSize; y++)
                {
                    //以中心作为起点，获取像素点向量
                    tempPixel.x = x - centerPixel.x;
                    tempPixel.y = y - centerPixel.y;
                    //是否在半径范围内
                    tempDisSqr = tempPixel.sqrMagnitude;
                    if (tempDisSqr >= minRadius * minRadius && tempDisSqr <= maxRadius * maxRadius)
                    {
                        //是否在角度范围内
                        tempAngle = Vector2.Angle(Vector2.up, tempPixel);
                        if (tempAngle < halfAngle || tempAngle > 360 - halfAngle)
                        {
                            //设置像素色值
                            texture2D.SetPixel(x, y, circleColor);
                            continue;
                        }
                    }
                    //设置为透明
                    texture2D.SetPixel(x, y, emptyColor);
                }
            }
        }
        else
        {
            //遍历像素点，绘制圆环
            for (int x = 0; x < spriteSize; x++)
            {
                for (int y = 0; y < spriteSize; y++)
                {
                    //以中心作为起点，获取像素点向量
                    tempPixel.x = x - centerPixel.x;
                    tempPixel.y = y - centerPixel.y;
                    //是否在半径范围内
                    tempDisSqr = tempPixel.sqrMagnitude;
                    if (tempDisSqr >= minRadius * minRadius && tempDisSqr <= maxRadius * maxRadius)
                    {
                        //设置像素色值
                        texture2D.SetPixel(x, y, circleColor);
                        continue;
                    }
                    //设置为透明
                    texture2D.SetPixel(x, y, emptyColor);
                }
            }
        }
        texture2D.Apply();
        //创建Sprite
        return Sprite.Create(texture2D, new Rect(0, 0, spriteSize, spriteSize), new Vector2(0.5f, 0.5f));
    }
}