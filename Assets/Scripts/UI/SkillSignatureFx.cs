using System;
using UnityEngine;
using UnityEngine.UI;
namespace ShadowTheater.UI
{
    /// <summary>Each skill combines authored delivery, impact formation, cadence and aftermath.</summary>
    public sealed class SkillSignatureFx : IDisposable
    {
        private readonly RectTransform root;
        private readonly Rect stageBounds;
        private readonly SkillPresentationProfile profile;
        private readonly Vector2 source,target,direction,normal;
        private readonly bool missed,self;
        private readonly Image seal,halo,core,emblem;
        private readonly Image[] traces=new Image[12],pieces;
        private readonly float angle;
        public SkillSignatureFx(RectTransform parent,SkillPresentationProfile profile,Vector2 source,Vector2 target,bool missed,bool self)
        {
            root=new GameObject("Signature_"+profile.VisualKey,typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent,false);root.anchorMin=root.anchorMax=new Vector2(.5f,.5f);root.sizeDelta=Vector2.zero;
            stageBounds=parent.rect;
            this.profile=profile;this.source=source;this.target=target;this.missed=missed;this.self=self;
            direction=(target-source).sqrMagnitude>1?(target-source).normalized:Vector2.right;
            normal=new Vector2(-direction.y,direction.x);angle=Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg;
            seal=Add("ChargeSeal",BattleFxGlyph.Ring);halo=Add("ImpactHalo",BattleFxGlyph.Ring);core=Add("Core",profile.glyph);
            emblem=Add("SkillEmblem",profile.glyph);
            for(int i=0;i<traces.Length;i++)traces[i]=Add("Delivery"+i,profile.glyph);
            int count=profile.IsUltimate?18:profile.IsRecovery?12:profile.kind==SkillPresentationKind.Basic?6:12;
            pieces=new Image[count];for(int i=0;i<count;i++)pieces[i]=Add("Formation"+i,profile.glyph);
            Sample(0);
        }
        private Image Add(string name,BattleFxGlyph glyph)
        {
            var image=new GameObject(name,typeof(RectTransform),typeof(Image)).GetComponent<Image>();image.transform.SetParent(root,false);
            image.sprite=BattleFxSpriteLibrary.Get(glyph);image.raycastTarget=false;image.preserveAspect=false;return image;
        }
        private static float Ease(float p)=>1-Mathf.Pow(1-Mathf.Clamp01(p),3);
        private static Vector2 Polar(float angle,float radius)=>new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
        private void Draw(Image image,Vector2 position,Vector2 size,float rotation,Color color,float alpha,BattleFxGlyph glyph)
        {
            image.enabled=alpha>.005f;if(!image.enabled)return;
            var art=profile.choreography.art;
            string id=image==seal?art.charge:image==core?art.impact:image==emblem || image==halo?art.crest:
                image.name.StartsWith("Delivery",StringComparison.Ordinal)?art.travel:art.debris;
            Sprite painted=SkillPartLibrary.Get(id);
            image.sprite=painted!=null?painted:BattleFxSpriteLibrary.Get(glyph);
            image.preserveAspect=painted!=null;
            if(painted!=null)
            {
                SkillPartLibrary.RecordDraw(id);
                // Preserve the shape of painted objects. Only rope-like travel is intentionally stretched.
                if(image.name.StartsWith("Formation",StringComparison.Ordinal))
                    size=Vector2.one*Mathf.Clamp(Mathf.Max(size.x,size.y)*.5f,24,74);
                if(image.name.StartsWith("Delivery",StringComparison.Ordinal))
                {
                    if(profile.choreography.delivery==SkillDelivery.Tether)image.preserveAspect=false;
                    else size=Vector2.one*Mathf.Clamp(Mathf.Max(size.x,size.y),65,210);
                }
                color=Color.Lerp(Color.white,color,.55f);
            }
            // Include rotated debris and travel art, not only the central emblem.
            float radians=rotation*Mathf.Deg2Rad,cos=Mathf.Abs(Mathf.Cos(radians)),sin=Mathf.Abs(Mathf.Sin(radians));
            Vector2 extent=new Vector2(cos*size.x+sin*size.y,sin*size.x+cos*size.y)*.5f;
            if(stageBounds.width>extent.x*2+24 && stageBounds.height>extent.y*2+24)
            {
                position.x=Mathf.Clamp(position.x,stageBounds.xMin+extent.x+12,stageBounds.xMax-extent.x-12);
                position.y=Mathf.Clamp(position.y,stageBounds.yMin+extent.y+12,stageBounds.yMax-extent.y-12);
            }
            image.rectTransform.anchoredPosition=position;
            image.rectTransform.sizeDelta=size;image.rectTransform.localRotation=Quaternion.Euler(0,0,rotation);
            color.a=Mathf.Clamp01(alpha);image.color=color;
        }
        private void Hide()
        {
            seal.enabled=halo.enabled=core.enabled=emblem.enabled=false;
            foreach(var image in traces)image.enabled=false;foreach(var image in pieces)image.enabled=false;
        }
        public void Sample(float progress)
        {
            Hide();float p=Mathf.Clamp01(progress);if(p<=0 || p>=1)return;
            if(profile.IsRecovery){Recovery(p);return;}
            var c=profile.choreography;
            float charge=Mathf.Sin(Mathf.Clamp01(p/profile.impactProgress)*Mathf.PI);
            float f=Mathf.InverseLerp(profile.windupProgress,profile.impactProgress,p);
            if(profile.kind!=SkillPresentationKind.Basic)
            {
                Draw(seal,source,Vector2.one*(60+charge*65),p*90+c.turn,profile.primary,charge*.5f,
                    c.delivery==SkillDelivery.Seal || c.delivery==SkillDelivery.Gate?BattleFxGlyph.Rune:BattleFxGlyph.Ring);
                if(p<profile.impactProgress)
                    Draw(emblem,source+Vector2.up*70,Vector2.one*(85+charge*40),0,profile.secondary,charge*.5f,profile.glyph);
            }
            if(p<profile.impactProgress)Delivery(p,f);
            if(!missed && p>=profile.impactProgress)
                Formation(Mathf.InverseLerp(profile.impactProgress,1,p),target,1);
        }
        private void Delivery(float p,float flight)
        {
            var c=profile.choreography;int count=c.delivery==SkillDelivery.Breath?10:
                c.delivery==SkillDelivery.Volley || c.delivery==SkillDelivery.Fan?c.beats*2:c.beats;
            count=Mathf.Clamp(count,1,traces.Length);
            Vector2 end=missed?target+direction*130+normal*70:target;
            for(int i=0;i<count;i++)
            {
                float delay=i*.025f;float f=Mathf.Clamp01((flight-delay)/(1-delay));
                Vector2 pos=Vector2.Lerp(source,end,Ease(f)),size=new Vector2(50,70);
                float rotation=angle-90+c.turn,alpha=Mathf.Sin(f*Mathf.PI)*.9f;
                BattleFxGlyph glyph=profile.glyph;
                switch(c.delivery)
                {
                    case SkillDelivery.Sweep:
                        pos=end+Polar((150-f*220+c.turn)*Mathf.Deg2Rad,90+i*6);size=new Vector2(30,170);
                        rotation=150-f*220+c.turn;glyph=BattleFxGlyph.Slash;break;
                    case SkillDelivery.Cross:
                        float sign=i%2==0?1:-1;pos=end+new Vector2((f-.5f)*190,sign*(f-.5f)*130);
                        size=new Vector2(25,160);rotation=sign*42+c.turn;glyph=BattleFxGlyph.Slash;break;
                    case SkillDelivery.Thrust:
                        pos=Vector2.Lerp(source,end,Mathf.Pow(f,2));size=new Vector2(28,110+i*10);break;
                    case SkillDelivery.Volley:
                        pos+=normal*((i-(count-1)*.5f)*22*Mathf.Sin(f*Mathf.PI));size=new Vector2(27,85);break;
                    case SkillDelivery.Arc:
                        pos+=Vector2.up*(Mathf.Sin(f*Mathf.PI)*(100+c.turn));rotation+=f*170;break;
                    case SkillDelivery.Breath:
                        pos=Vector2.Lerp(source,end,f)+normal*(Mathf.Sin(i*2.4f)*(15+f*85));size=Vector2.one*(25+f*60);
                        alpha*=.65f;rotation+=i*45;break;
                    case SkillDelivery.Descend:
                        pos=end+new Vector2((i-(count-1)*.5f)*40,260*(1-Ease(f)));size=new Vector2(40,100+f*80);rotation=c.turn;break;
                    case SkillDelivery.Rush:
                        pos=Vector2.Lerp(source,end,f)-direction*i*22;size=new Vector2(32,120);alpha*=1-i*.12f;break;
                    case SkillDelivery.Ripple:
                        pos=source+direction*f*Vector2.Distance(source,end);size=new Vector2(40+f*240,40+f*190);
                        glyph=BattleFxGlyph.Ring;rotation=c.turn;break;
                    case SkillDelivery.Orbit:
                        pos=Vector2.Lerp(source,end,f)+Polar((f*480+i*360f/count+c.turn)*Mathf.Deg2Rad,Mathf.Sin(f*Mathf.PI)*110);
                        rotation+=f*300;break;
                    case SkillDelivery.Seal:
                        pos=end+Polar((i*360f/count+c.turn)*Mathf.Deg2Rad,70*(1-f));size=Vector2.one*(40+f*80);
                        glyph=BattleFxGlyph.Rune;rotation=f*180+c.turn;break;
                    case SkillDelivery.Tether:
                        pos=(source+end)*.5f+normal*(i-(count-1)*.5f)*16;size=new Vector2(30,Vector2.Distance(source,end)*f);
                        glyph=BattleFxGlyph.Thread;alpha=Mathf.Sin(f*Mathf.PI)*.9f;break;
                    case SkillDelivery.Gate:
                        pos=end+new Vector2((i%2==0?1:-1)*(160-100*f),((i/2)%3-1)*60);
                        size=new Vector2(45,190*f);glyph=BattleFxGlyph.Rune;rotation=c.turn;break;
                    case SkillDelivery.Column:
                        pos=end+Vector2.up*50;size=new Vector2(25+f*35,380*f);glyph=BattleFxGlyph.Thread;
                        rotation=c.turn;alpha*=.8f;break;
                    case SkillDelivery.Fan:
                        pos=Vector2.Lerp(source,end,f)+Polar((20+i*140f/count+c.turn)*Mathf.Deg2Rad,Mathf.Sin(f*Mathf.PI)*140);
                        rotation=c.turn+i*120f/count;size=new Vector2(35,115);break;
                    case SkillDelivery.Siphon:
                        pos=Vector2.Lerp(end,source,f)+normal*Mathf.Sin(f*Mathf.PI)*35;size=Vector2.one*(80-55*f);rotation-=f*240;break;
                    default:pos+=Vector2.up*Mathf.Sin(f*Mathf.PI)*75;break;
                }
                Draw(traces[i],pos,size*profile.strength,rotation, i%2==0?profile.primary:profile.secondary,alpha,glyph);
            }
        }
        private void Recovery(float p)
        {
            var c=profile.choreography;float gather=Mathf.SmoothStep(0,1,Mathf.Clamp01(p/profile.impactProgress));
            float tail=Mathf.InverseLerp(profile.impactProgress,1,p),fade=Mathf.Sin(p*Mathf.PI);
            Vector2 center=self?source:target;float radius=profile.IsUltimate?155:100;
            Draw(seal,center+Vector2.down*85,new Vector2(radius*2,40+gather*30),c.turn,profile.primary,fade*.65f,BattleFxGlyph.Ring);
            Draw(core,center+Vector2.up*12,Vector2.one*(profile.IsUltimate?195:135)*(.8f+gather*.2f),0,
                profile.secondary,fade*.9f,profile.glyph);
            for(int i=0;i<2;i++)
                Draw(traces[i],center+new Vector2((i==0?-1:1)*(40*(1-gather)+15),-45+p*90),
                    new Vector2(95,150),i==0?-12:12,profile.primary,fade*.65f,BattleFxGlyph.Thread);
            if(profile.IsUltimate)
                Draw(emblem,center+Vector2.up*100,Vector2.one*(150+gather*65),0,profile.secondary,fade*.5f,BattleFxGlyph.Shield);
            else
                Draw(emblem,center+Vector2.up*85,Vector2.one*90,0,profile.secondary,fade*tail*.55f,profile.glyph);
            for(int i=0;i<pieces.Length;i++)
            {
                float t=i/(float)pieces.Length,a=t*Mathf.PI*2+c.turn*Mathf.Deg2Rad;
                Vector2 pos=Polar(a,radius*(1-gather*.75f));Vector2 size=Vector2.one*22;
                float rotation=a*Mathf.Rad2Deg+p*90;BattleFxGlyph glyph=profile.glyph;
                switch(c.delivery)
                {
                    case SkillDelivery.Weave:
                        pos=new Vector2((i%2==0?-1:1)*(95*(1-gather)+15),(i/2%6-2.5f)*25);
                        size=new Vector2(24,55+gather*25);glyph=BattleFxGlyph.Thread;rotation=i%2==0?55:-55;break;
                    case SkillDelivery.Shelter:
                        pos=Polar((25+t*310)*Mathf.Deg2Rad,radius*(.7f+.3f*gather));size=new Vector2(32,75);
                        glyph=c.formation==SkillFormation.Pages?BattleFxGlyph.Rune:BattleFxGlyph.Shield;rotation=t*360-90;break;
                    case SkillDelivery.Bloom:
                        pos=Polar(a+p*1.8f,25+gather*radius*.8f)+Vector2.up*(tail*90-40);
                        size=new Vector2(28,50);glyph=c.formation==SkillFormation.Crown?BattleFxGlyph.Flame:profile.glyph;break;
                    case SkillDelivery.Ripple:
                        pos=new Vector2(0,-65+(i%5)*35+tail*60);size=new Vector2(80+gather*120+i%3*20,35);
                        glyph=BattleFxGlyph.Ring;rotation=c.turn*.1f;break;
                    case SkillDelivery.Orbit:
                    case SkillDelivery.Fan:
                        pos=Polar(a+p*3.4f,radius*(1-.65f*gather))+Vector2.up*tail*90;size=new Vector2(25,65);break;
                    default:
                        pos=new Vector2(Mathf.Sin(a)*radius*.65f,-110+Mathf.Repeat(p*(90+c.beats*30)+i*17,220));
                        size=Vector2.one*(15+i%3*8);glyph=BattleFxGlyph.Star;break;
                }
                if(c.formation==SkillFormation.Eclipse && i==0)
                {pos=Vector2.up*(80+gather*25);size=Vector2.one*(100+gather*40);glyph=BattleFxGlyph.Crescent;rotation=c.turn;}
                if(c.formation==SkillFormation.Mask && i<2)
                {pos=new Vector2((i==0?-1:1)*35,65+gather*25);glyph=BattleFxGlyph.Petal;size=new Vector2(22,50);rotation=i==0?-25:25;}
                if(c.formation==SkillFormation.Bell && i%4==0)
                {glyph=BattleFxGlyph.Ring;size=new Vector2(90+gather*60,30);pos.y+=20;}
                float breath=.7f+.3f*Mathf.Sin(p*Mathf.PI*c.beats+i*.4f);
                Draw(pieces[i],center+pos,size*profile.strength,rotation,i%3==0?profile.secondary:profile.primary,fade*breath,glyph);
            }
        }
        public void SamplePrelude(float p)
        {
            Hide();float reveal=Mathf.Sin(Mathf.Clamp01(p)*Mathf.PI*.5f);
            if(profile.IsRecovery){Recovery(p*.45f);return;}
            Formation(.04f+p*.15f,target,1.15f, reveal*.38f);
        }
        private void Formation(float q,Vector2 center,float scale,float visibility=1)
        {
            var c=profile.choreography;float e=Ease(q),fade=Mathf.Pow(1-q,.85f)*visibility;
            float strength=profile.strength*scale;int count=pieces.Length;
            bool dark=c.formation==SkillFormation.Void || c.formation==SkillFormation.Eclipse;
            BattleFxGlyph coreGlyph=dark?BattleFxGlyph.Disc:c.formation==SkillFormation.Crown?BattleFxGlyph.Crown:
                c.formation==SkillFormation.Mask?BattleFxGlyph.Mask:c.formation==SkillFormation.Bell?BattleFxGlyph.Bell:
                c.formation==SkillFormation.Shield?BattleFxGlyph.Shield:profile.glyph;
            float coreSize=profile.IsUltimate?180:profile.kind==SkillPresentationKind.Basic?95:140;
            float coreTurn=c.formation==SkillFormation.Edge || c.formation==SkillFormation.Jaw?c.turn:0;
            Draw(core,center,Vector2.one*coreSize*strength*(.85f+e*.15f),coreTurn,
                profile.secondary,fade*.92f,coreGlyph);
            if(profile.IsUltimate)
                Draw(emblem,center+Vector2.up*85,Vector2.one*215*strength,0,profile.secondary,fade*.55f,profile.glyph);
            for(int i=0;i<count;i++)
            {
                float unit=i/(float)count,a=unit*Mathf.PI*2;
                float delay=(i%c.beats)*.045f,t=Mathf.Clamp01((q-delay)/(1-delay)),spread=Ease(t);
                Vector2 pos=Polar(a,35+spread*90),size=new Vector2(25,70);
                float rotation=unit*360;BattleFxGlyph glyph=profile.glyph;
                switch(c.formation)
                {
                    case SkillFormation.Edge:
                        pos=new Vector2((i%c.beats-(c.beats-1)*.5f)*35+(spread-.5f)*95,(spread-.5f)*70+(i/c.beats-count/c.beats/2)*3);
                        size=new Vector2(28,190-i/c.beats*8);rotation=-35+i%c.beats*18;glyph=BattleFxGlyph.Slash;break;
                    case SkillFormation.Jaw:
                        pos=new Vector2((i%(count/2)-(count/2-1)*.5f)*13,(i<count/2?1:-1)*(80-55*spread));
                        size=new Vector2(28,75);rotation=i<count/2?180:0;glyph=BattleFxGlyph.Crystal;break;
                    case SkillFormation.Crown:
                        float crownX=(i%5-2)*43;pos=new Vector2(crownX,50+Mathf.Abs(crownX)*.22f+(1-spread)*100);
                        size=new Vector2(36,80+Mathf.Abs(i%5-2)*18);rotation=(i%5-2)*-12;break;
                    case SkillFormation.Prism:
                        pos=Polar(a,55+spread*35);size=new Vector2(30,110);rotation=unit*360-90;glyph=BattleFxGlyph.Crystal;break;
                    case SkillFormation.Sigil:
                        pos=Polar(a+q,85);size=Vector2.one*(25+i%3*8);rotation=unit*360+q*90;glyph=BattleFxGlyph.Rune;break;
                    case SkillFormation.Rain:
                        pos=new Vector2((i%6-2.5f)*40,180-spread*310+i/6*25);size=new Vector2(28,110);rotation=10;break;
                    case SkillFormation.Tide:
                        pos=new Vector2((spread-.5f)*220+(i%3-1)*45,(i/3%4-1.5f)*35+Mathf.Sin(q*7+i)*15);
                        size=new Vector2(115,65);rotation=q*100;glyph=BattleFxGlyph.Crescent;break;
                    case SkillFormation.Cage:
                        pos=new Vector2((i%6-2.5f)*(52-spread*20),(i/6%3-1)*18);size=new Vector2(30,260);
                        rotation=i%2==0?18:-18;glyph=BattleFxGlyph.Thread;break;
                    case SkillFormation.Void:
                        pos=Polar(a+q*4,150*(1-spread*.8f));size=Vector2.one*(20+i%3*14);rotation=unit*360+q*250;break;
                    case SkillFormation.Rays:
                        pos=Polar(a,45+spread*45);size=new Vector2(24,180);rotation=unit*360-90;glyph=BattleFxGlyph.Star;break;
                    case SkillFormation.Suture:
                        pos=new Vector2((i%2==0?-1:1)*(65*(1-spread)+15),(i/2%6-2.5f)*30);
                        size=new Vector2(26,100);rotation=i%2==0?50:-50;glyph=BattleFxGlyph.Thread;break;
                    case SkillFormation.Blossom:
                        pos=Polar(a+q*3,30+spread*120);size=new Vector2(30,60);rotation=unit*360+q*170;glyph=BattleFxGlyph.Petal;break;
                    case SkillFormation.Stars:
                        int node=i%7;Vector2 point=Polar(node*2.39996f,50+node%3*35),previous=Polar((node+1)*2.39996f,50+(node+1)%3*35);
                        if(i%3==0){pos=point;size=Vector2.one*(25+node%3*10);glyph=BattleFxGlyph.Star;}
                        else{pos=(point+previous)*.5f;size=new Vector2(18,Vector2.Distance(point,previous));glyph=BattleFxGlyph.Thread;rotation=Mathf.Atan2(previous.y-point.y,previous.x-point.x)*Mathf.Rad2Deg-90;}
                        break;
                    case SkillFormation.Shield:
                        pos=Polar(a,95);size=new Vector2(45,90);rotation=unit*360-90;glyph=BattleFxGlyph.Shield;break;
                    case SkillFormation.Bell:
                        pos=new Vector2(0,(i%c.beats-(c.beats-1)*.5f)*28);size=new Vector2(100+spread*160+i%3*25,90+spread*110);
                        rotation=i%2*15;glyph=BattleFxGlyph.Ring;break;
                    case SkillFormation.Wings:
                        pos=new Vector2((i%2==0?-1:1)*(45+spread*(55+i/2*6)),Mathf.Sin(unit*Mathf.PI)*80-i/2*3);
                        size=new Vector2(40,130);rotation=i%2==0?-45-i/2*2:45+i/2*2;glyph=BattleFxGlyph.Feather;break;
                    case SkillFormation.Pages:
                        pos=new Vector2((i%6-2.5f)*42+(1-spread)*(i%2==0?-100:100),(i/6%5-2)*42);
                        size=new Vector2(35,50);rotation=i%2==0?-15:15;glyph=BattleFxGlyph.Rune;break;
                    case SkillFormation.Clock:
                        pos=Polar(a,100);size=Vector2.one*22;glyph=BattleFxGlyph.Rune;
                        if(i<2){pos=Vector2.zero;size=new Vector2(24,180-i*50);rotation=q*(i==0?720:-360);glyph=BattleFxGlyph.Thread;}
                        break;
                    case SkillFormation.Mask:
                        pos=new Vector2((i%2==0?-1:1)*(45+spread*60),(i/2%5-2)*35);
                        size=new Vector2(30,70);rotation=i%2==0?-30:30;glyph=BattleFxGlyph.Crystal;break;
                    case SkillFormation.Eclipse:
                        pos=Polar(a+q*2,95+Mathf.Sin(unit*12)*15);size=Vector2.one*(40+i%3*12);
                        rotation=unit*360+q*90;glyph=BattleFxGlyph.Crescent;break;
                }
                switch(c.finish)
                {
                    case SkillFinish.Embers:pos+=Vector2.up*(q*q*130);size*=1-q*.35f;break;
                    case SkillFinish.Shatter:pos+=Polar(a,q*q*110);rotation+=q*q*(i%2==0?140:-140);break;
                    case SkillFinish.Collapse:pos*=1-e*.8f;size*=1-q*.5f;break;
                    case SkillFinish.Ascend:pos+=Vector2.up*q*q*170;break;
                    case SkillFinish.Return:center=Vector2.Lerp(target,source,q*q*.35f);pos*=1-q*.4f;break;
                    case SkillFinish.Knit:pos*=1-e*.9f;break;
                    case SkillFinish.Scatter:pos*=1+q*q*.65f;break;
                }
                pos=Quaternion.Euler(0,0,c.turn)*(Vector3)pos;
                float cadence=.7f+.3f*Mathf.Cos(q*Mathf.PI*c.beats);
                float alpha=fade*Mathf.Clamp01(t*40)*cadence*(c.finish==SkillFinish.Fade?1-q:1);
                Draw(pieces[i],center+pos*strength,size*strength,rotation+c.turn,i%3==0?profile.secondary:profile.primary,alpha,glyph);
            }
        }
        public void Dispose()
        {
            if(root==null)return;
            if(Application.isPlaying)UnityEngine.Object.Destroy(root.gameObject);else UnityEngine.Object.DestroyImmediate(root.gameObject);
        }
    }
}
